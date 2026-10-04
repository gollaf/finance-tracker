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

builder.Services
    .AddApplication()
    .AddInfrastructure(builder.Configuration);

// Enums travel over HTTP as names ("Checking"), not numbers.
builder.Services.AddControllers().AddJsonOptions(options =>
{
    options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
});

builder.Services.AddOpenApi();

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

app.UseExceptionHandler();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.MapScalarApiReference();
}

// Liveness runs no checks, so a database problem never fails it.
app.MapHealthChecks("/health/live", new HealthCheckOptions
{
    Predicate = _ => false
});

// Readiness: the database is reachable and fully migrated.
app.MapHealthChecks("/health/ready", new HealthCheckOptions
{
    Predicate = check => check.Tags.Contains("ready")
});

app.MapControllers();

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
