using FinanceTracker.Application;
using FinanceTracker.Infrastructure;
using FinanceTracker.Infrastructure.Persistence;
using Microsoft.AspNetCore.Diagnostics;
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

var app = builder.Build();

// Applies any pending EF Core migration against whatever database
// ConnectionStrings:FinanceTracker points at, every time this host starts.
// This is what lets `docker compose up` produce a fully migrated database
// with zero manual steps -- see ADR 0008 for the full reasoning, and the
// caveat it records for once more than one instance of this API is ever
// running at the same time (Phase 6, Kubernetes).
using (var migrationScope = app.Services.CreateScope())
{
    var dbContext = migrationScope.ServiceProvider.GetRequiredService<FinanceTrackerDbContext>();
    await dbContext.Database.MigrateAsync();
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

app.MapControllers();

app.Run();

// Exposes the top-level Program so FinanceTracker.Api.IntegrationTests'
// CustomWebApplicationFactory : WebApplicationFactory<Program> can boot
// this exact host in tests.
public partial class Program;
