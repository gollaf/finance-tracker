using FinanceTracker.Worker;

// The Worker's composition root -- the counterpart of Api/Program.cs. It
// wires up the exact same Application and Infrastructure layers the Api
// does; the only difference is the entry point: the Api turns HTTP
// requests into MediatR requests, this process turns RabbitMQ messages
// into them. See docs/adr/0011-async-messaging-rabbitmq-raw-client.md.
//
// Host.CreateApplicationBuilder is the non-web equivalent of
// WebApplication.CreateBuilder: configuration (appsettings.json, User
// Secrets in Development, environment variables), logging, and DI -- but
// no web server. Its environment comes from DOTNET_ENVIRONMENT, not
// ASPNETCORE_ENVIRONMENT, and defaults to Production when that is unset.
var builder = Host.CreateApplicationBuilder(args);

// Every registration lives in AddWorker, shared with the Worker's
// end-to-end tests -- see WorkerServiceCollectionExtensions.
builder.Services.AddWorker(builder.Configuration);

var host = builder.Build();

// Unlike the Api, this process never applies EF Core migrations -- the Api
// alone owns that (ADR 0008), so two processes never race to migrate the
// same database at startup.
host.Run();
