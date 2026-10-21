using FinanceTracker.Infrastructure.Outbox;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Npgsql;
using OpenTelemetry;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using RabbitMQ.Client;

namespace FinanceTracker.Infrastructure.Observability
{
    /// <summary>
    /// Traces, metrics and logs through OpenTelemetry, shared by the Api and
    /// the Worker. Each entry point adds what only it has -- the Api its
    /// incoming HTTP requests -- to the builder this returns.
    /// </summary>
    /// <remarks>
    /// Telemetry is collected everywhere but leaves the process only when
    /// OTEL_EXPORTER_OTLP_ENDPOINT is set. Without it the OTLP exporter would
    /// still send to its default address, localhost:4317, and log a failed
    /// export every few seconds on every machine that has no collector.
    /// </remarks>
    public static class TelemetrySetup
    {
        public const string OtlpEndpointKey = "OTEL_EXPORTER_OTLP_ENDPOINT";

        public static OpenTelemetryBuilder AddTelemetry(this IHostApplicationBuilder builder, string serviceName)
        {
            var telemetry = builder.Services.AddOpenTelemetry()
                .ConfigureResource(resource => resource
                    .AddService(serviceName)
                    .AddAttributes([new("deployment.environment.name", builder.Environment.EnvironmentName)]))
                .WithTracing(tracing => tracing
                    .AddHttpClientInstrumentation()
                    .AddNpgsql()
                    .AddSource(RabbitMQActivitySource.PublisherSourceName, RabbitMQActivitySource.SubscriberSourceName)
                    .AddSource(OutboxRelay.ActivitySourceName))
                .WithMetrics(metrics => metrics
                    .AddHttpClientInstrumentation()
                    .AddRuntimeInstrumentation()
                    .AddNpgsqlInstrumentation())
                .WithLogging(null, options =>
                {
                    // The rendered message as well as its template and
                    // values, and the request's scopes, as the console shows.
                    options.IncludeFormattedMessage = true;
                    options.IncludeScopes = true;
                });

            // One call covers all three signals; the protocol and address
            // come from the standard OTEL_EXPORTER_OTLP_* settings.
            if (!string.IsNullOrWhiteSpace(builder.Configuration[OtlpEndpointKey]))
                telemetry.UseOtlpExporter();

            return telemetry;
        }
    }
}
