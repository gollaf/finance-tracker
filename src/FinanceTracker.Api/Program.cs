using FinanceTracker.Api.HealthChecks;
using FinanceTracker.Application;
using FinanceTracker.Infrastructure;
using FinanceTracker.Infrastructure.Persistence;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.EntityFrameworkCore;
using Scalar.AspNetCore;
using System.Text.Json.Serialization;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services
    .AddApplication()
    .AddInfrastructure(builder.Configuration);

// AddJsonOptions applies to both request binding and response
// serialization -- so, e.g., AccountType travels over HTTP as
// "Checking" rather than an unreadable integer, matching the same
// readability call ADR 0003 already made for how enums are stored.
builder.Services.AddControllers().AddJsonOptions(options =>
{
    options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
});

// AddOpenApi generates the machine-readable API description (served at
// /openapi/v1.json below) straight from this same JsonOptions
// configuration, MVC route/model metadata, and XML doc comments if enabled
// later -- there's no separate schema-generation setup to keep in sync
// with the JSON options above the way older Swagger tooling needed.
builder.Services.AddOpenApi();

// Backs app.UseExceptionHandler() below: any exception that isn't already
// a Result failure -- a genuine bug, per ADR 0004 -- gets turned into a
// 500 ProblemDetails response that never leaks exception details, except
// in Development, where the exception's own ToString() is attached so
// local debugging still works without needing a debugger attached.
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

// Two separate health endpoints, not one, because they answer two different
// questions a container orchestrator asks -- see ADR 0009. Both database
// checks are tagged "ready" so they can be selected independently below;
// they are never wired into the liveness endpoint. "migrations" is what
// keeps an instance out of rotation while its database is reachable but
// not yet migrated -- see PendingMigrationsHealthCheck.
builder.Services.AddHealthChecks()
    .AddDbContextCheck<FinanceTrackerDbContext>("database", tags: ["ready"])
    .AddCheck<PendingMigrationsHealthCheck>("migrations", tags: ["ready"]);

var app = builder.Build();

// `dotnet FinanceTracker.Api.dll migrate` applies any pending EF Core
// migration and exits without ever starting the web server. That lets a
// deployment run migrations exactly once, as a step of its own, instead of
// inside every instance's startup -- see ADR 0017. The verb deliberately
// has no leading dashes: the command-line configuration provider ignores a
// bare word, so it never turns into a stray configuration key.
if (args is ["migrate", ..])
{
    await ApplyMigrationsAsync(app.Services);
    app.Logger.LogInformation("Database schema is up to date; exiting because the 'migrate' command was given.");
    return;
}

// Everywhere else -- `dotnet run`, docker compose, the integration tests --
// the Api still applies pending migrations on every startup, so a fresh
// database needs no manual step (ADR 0008). A deployment that runs
// `migrate` as its own step turns this off with
// Database__ApplyMigrationsOnStartup=false. Read from app.Configuration,
// after Build(), rather than builder.Configuration: configuration that
// WebApplicationFactory-based tests add is only applied during Build().
if (app.Configuration.GetValue("Database:ApplyMigrationsOnStartup", defaultValue: true))
{
    await ApplyMigrationsAsync(app.Services);
}

app.UseExceptionHandler();

// Both the raw OpenAPI document and Scalar's browsable UI over it are
// Development-only, same reasoning as the exception detail above: an
// API's exact shape is useful while building against it locally, not
// something to expose by default once deployed.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.MapScalarApiReference();
}

// Liveness: is this process itself still able to handle a request at
// all? Predicate = _ => false runs zero registered checks, so this can
// never fail because a dependency (the database) is unhappy -- exactly
// what a Kubernetes liveness probe should ask, per ADR 0009.
app.MapHealthChecks("/health/live", new HealthCheckOptions
{
    Predicate = _ => false
});

// Readiness: can this instance currently serve real traffic? Runs only
// the checks tagged "ready": the database is reachable, and its schema has
// every migration this build expects.
app.MapHealthChecks("/health/ready", new HealthCheckOptions
{
    Predicate = check => check.Tags.Contains("ready")
});

app.MapControllers();

app.Run();

// Safe to call repeatedly and concurrently: MigrateAsync applies only the
// migrations not yet recorded in __EFMigrationsHistory, and EF Core holds a
// database-wide lock while it does, so two processes calling it at the same
// moment run one after the other rather than both applying the same one.
static async Task ApplyMigrationsAsync(IServiceProvider services)
{
    using var scope = services.CreateScope();
    var dbContext = scope.ServiceProvider.GetRequiredService<FinanceTrackerDbContext>();
    await dbContext.Database.MigrateAsync();
}

// Exposes the top-level Program so FinanceTracker.Api.IntegrationTests'
// CustomWebApplicationFactory : WebApplicationFactory<Program> can boot
// this exact host in tests.
public partial class Program;
