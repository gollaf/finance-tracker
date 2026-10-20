using FinanceTracker.Infrastructure.Observability;
using FinanceTracker.Worker;

// The environment comes from DOTNET_ENVIRONMENT, not ASPNETCORE_ENVIRONMENT.
var builder = Host.CreateApplicationBuilder(args);

// Shared with the end-to-end tests.
builder.Services.AddWorker(builder.Configuration);

builder.AddTelemetry("finance-tracker-worker");

var host = builder.Build();

// Migrations are the Api's job, never the Worker's.
host.Run();
