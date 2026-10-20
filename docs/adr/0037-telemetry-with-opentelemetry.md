# 37. Telemetry with OpenTelemetry

## Status

Accepted.

## Context

The services' only output is their console log. On AWS it is searchable
as JSON ([ADR 0035](./0035-monitoring-on-aws-with-cloudwatch.md)), but a
slow or failing request still can't be followed from the Api into the
database, and there are no metrics from inside the processes: request
durations, garbage collection, connection pools.

The Azure sprint sends telemetry to Application Insights. The code should
not depend on that: the same telemetry should be viewable locally, and
could go to any other backend later, on the server or on another cloud.

OpenTelemetry is the vendor-neutral standard for this. Applications
record three signals -- traces (a tree of timed spans per operation),
metrics and logs -- through one SDK, and an exporter sends them, usually
over OTLP, OpenTelemetry's own protocol. .NET itself already records
spans (`Activity`) and metrics (`Meter`) in ASP.NET Core, HttpClient,
Npgsql and RabbitMQ.Client; the SDK collects them.

## Decision

1. **The OpenTelemetry SDK in both entry points**, set up once in
   `Infrastructure/Observability/TelemetrySetup.cs` and called from each
   `Program.cs` with the service's name (`finance-tracker-api`,
   `finance-tracker-worker`). The resource also carries the environment
   name (`deployment.environment.name`).
2. **What is recorded:**
   - traces: incoming HTTP requests (Api only), outgoing HTTP calls (Groq),
     database commands (Npgsql), and publishing and consuming messages
     (RabbitMQ.Client's own ActivitySources, no extra package);
   - metrics: ASP.NET Core (Api only), HttpClient, the .NET runtime, and
     Npgsql's connection pool and commands;
   - logs: every `ILogger` entry, with its formatted message and scopes,
     in addition to the console.
3. **Export only when configured.** The OTLP exporter is added when
   `OTEL_EXPORTER_OTLP_ENDPOINT` is set, the standard OpenTelemetry
   setting, which also selects the protocol and address. Unset, nothing
   leaves the process; the server, Kubernetes and AWS run as before.
4. **Noise is left out at the source.**
   - Requests to `/health/...` are not traced: orchestrators call them
     every few seconds, and the readiness check queries the database each
     time.
   - Database commands are traced only inside another span (a request, a
     consumed message). The outbox relay polls the database every few
     seconds outside any span, and each poll would otherwise be a trace of
     its own. Startup migrations are left out for the same reason.
5. **The Aspire Dashboard locally.** The root `docker-compose.yml` runs
   Microsoft's standalone Aspire Dashboard, which receives OTLP and shows
   traces, metrics and structured logs at `http://localhost:18888`. Its
   image is pinned to a version, like the other images. It runs without
   sign-in and keeps everything in memory, which suits local development
   only.

## Consequences

**Positive:**

- A request can be followed through the Api's code and its database
  queries in one view, with timings.
- The application code names no telemetry backend; switching backends is
  configuration, plus at most one exporter.
- Nothing changes for deployments that don't set the endpoint.

**Negative:**

- **A trace stops at the outbox.** The relay publishes an event later, in
  a loop of its own, so the Worker's handling of a message isn't part of
  the request's trace yet. Carrying the trace context through the outbox
  is a separate change.
- **More packages** in Infrastructure and the Api, all from the
  OpenTelemetry project or Npgsql, to keep up to date.
- **Database tracing depends on a span being active.** Work started
  outside any span, like a new background loop, runs untraced until it
  starts a span of its own.
- **The dashboard is not a store**: a restart empties it.

## Alternatives Considered

- **The Azure Monitor OpenTelemetry Distro in the app** -- one call that
  sets everything up, but it ties the code to Azure and sends nothing
  anywhere else. An Azure Monitor exporter can be added beside OTLP when
  needed.
- **Vendor SDKs** (the classic Application Insights SDK, AWS X-Ray SDK) --
  one backend each, and Microsoft points new applications to OpenTelemetry.
- **EF Core's OpenTelemetry instrumentation** -- still a prerelease
  package, and Npgsql's spans already show each SQL statement and its
  timing.
- **Jaeger, or Grafana with Tempo, locally** -- traces only, or several
  containers; the Aspire Dashboard shows all three signals in one.
- **Exporting always, to localhost by default** -- a failed export logged
  every few seconds wherever no collector runs.
