using FinanceTracker.Api.HealthChecks;
using FinanceTracker.Api.OpenApi;
using FinanceTracker.Api.RateLimiting;
using FinanceTracker.Api.ReverseProxy;
using FinanceTracker.Application;
using FinanceTracker.Infrastructure;
using FinanceTracker.Infrastructure.Persistence;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Scalar.AspNetCore;
using System.Text.Json.Serialization;

var builder = WebApplication.CreateBuilder(args);

builder.Services
    .AddApplication()
    .AddInfrastructure(builder.Configuration);

// Enums travel over HTTP as names ("Checking"), not numbers.
builder.Services.AddControllers().AddJsonOptions(options =>
{
    options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
});

builder.Services.AddOpenApi(options => options.AddDocumentTransformer<BearerSecuritySchemeTransformer>());

// Unhandled exceptions become a 500 ProblemDetails; exception details are
// included in Development only.
builder.Services.AddProblemDetails(options =>
{
    options.CustomizeProblemDetails = context =>
    {
        if (!builder.Environment.IsDevelopment())
            return;

        var exception = context.HttpContext.Features.Get<IExceptionHandlerFeature>()?.Error;
        if (exception is not null)
            context.ProblemDetails.Extensions["exception"] = exception.ToString();
    };
});

// Checks tagged "ready" back /health/ready only; see ADR 0009.
builder.Services.AddHealthChecks()
    .AddDbContextCheck<FinanceTrackerDbContext>("database", tags: ["ready"])
    .AddCheck<PendingMigrationsHealthCheck>("migrations", tags: ["ready"]);

// Users and sign-in tokens. The tokens are encrypted with Data
// Protection keys, which by default live inside the container: a redeploy
// would invalidate every token, and a second instance couldn't read the
// first one's. Keeping the keys in the database avoids both.
builder.Services.AddIdentityApiEndpoints<IdentityUser<Guid>>()
    .AddEntityFrameworkStores<FinanceTrackerDbContext>();

builder.Services.AddDataProtection()
    .SetApplicationName("FinanceTracker")
    .PersistKeysToDbContext<FinanceTrackerDbContext>();

builder.Services.AddReverseProxySupport();
builder.Services.AddApiRateLimiting(builder.Configuration);

var app = builder.Build();

// `dotnet FinanceTracker.Api.dll migrate` applies pending migrations and
// exits without starting the web server (ADR 0017). No leading dashes, so
// the command-line configuration provider ignores it.
if (args is ["migrate", ..])
{
    await ApplyMigrationsAsync(app.Services);
    app.Logger.LogInformation("Database schema is up to date; exiting because the 'migrate' command was given.");
    return;
}

// On by default, so a fresh database needs no manual step. A deployment
// that runs `migrate` separately sets Database__ApplyMigrationsOnStartup=false.
// Read after Build(): configuration added by WebApplicationFactory tests
// is only applied then.
if (app.Configuration.GetValue("Database:ApplyMigrationsOnStartup", defaultValue: true))
{
    await ApplyMigrationsAsync(app.Services);
}

// First, so everything after it, the rate limiter included, sees the
// visitor's address rather than the reverse proxy's.
app.UseForwardedHeaders();

app.UseExceptionHandler();

// Before authentication: a flood is turned away before any token is decrypted.
app.UseRateLimiter();

app.UseAuthentication();
app.UseAuthorization();

// API docs default to Development only. A public demo can turn them on with
// ApiDocs__Enabled=true: they describe the endpoints, nothing secret.
if (app.Configuration.GetValue("ApiDocs:Enabled", defaultValue: app.Environment.IsDevelopment()))
{
    app.MapOpenApi();
    app.MapScalarApiReference();
}

// Liveness runs no checks, so a database problem never fails it. Neither
// probe is rate limited: an orchestrator polls them every few seconds, and a
// rejected probe would look like a failing instance.
app.MapHealthChecks("/health/live", new HealthCheckOptions
{
    Predicate = _ => false
}).DisableRateLimiting();

// Readiness: the database is reachable and fully migrated.
app.MapHealthChecks("/health/ready", new HealthCheckOptions
{
    Predicate = check => check.Tags.Contains("ready")
}).DisableRateLimiting();

app.MapGroup("/api/auth")
    .WithTags("Auth")
    .RequireRateLimiting(RateLimitingSetup.AuthPolicy)
    .MapIdentityApi<IdentityUser<Guid>>();

// Sign-in is required per endpoint group, not with a global fallback policy:
// MapIdentityApi's /login and /register carry no AllowAnonymous, so a
// fallback policy would lock callers out of signing in at all. Health checks
// and the API docs stay open.
app.MapControllers().RequireAuthorization();

app.Run();

// Safe to run concurrently: EF Core takes a database lock while migrating.
static async Task ApplyMigrationsAsync(IServiceProvider services)
{
    using var scope = services.CreateScope();
    var dbContext = scope.ServiceProvider.GetRequiredService<FinanceTrackerDbContext>();
    await dbContext.Database.MigrateAsync();
}

// For WebApplicationFactory<Program> in the integration tests.
public partial class Program;
