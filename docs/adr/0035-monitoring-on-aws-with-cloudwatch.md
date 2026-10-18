# 35. Monitoring on AWS with CloudWatch

## Status

Accepted. Builds on [ADR 0033](./0033-services-on-ecs-discovery-migrations-and-secrets.md)
and [ADR 0034](./0034-public-https-through-cloudfront-and-an-internal-load-balancer.md).

## Context

The services' output already reaches CloudWatch Logs (ADR 0033), but as
plain text: finding the errors means reading through everything, and no
one learns about a failure until they look. On the server, problems show
up in `docker compose logs` and the readiness check (ADR 0031); on AWS
the system should say on its own when it breaks.

The app has no OpenTelemetry instrumentation. Adding it is planned for the
Azure sprint, where Application Insights consumes it; this decision covers
what CloudWatch can do without it.

## Decision

1. **JSON logs, switched on by configuration.** The .NET containers on AWS
   set `Logging__Console__FormatterName=json` and include scopes. Each log
   line becomes a JSON object (`LogLevel`, `Category`, `Message`, and the
   request's scopes), which CloudWatch Logs Insights and metric filters can
   query by field. The application code doesn't change, and the server
   keeps its readable text logs.
2. **Errors become metrics.** A metric filter per service counts the lines
   logged at `Error` or `Critical` (`FinanceTracker/ApiErrors`,
   `FinanceTracker/WorkerErrors`).
3. **Five alarms**, each notifying an SNS topic when it goes off and when
   it clears:
   - no healthy Api task behind the load balancer for 3 minutes (no data
     counts as unhealthy);
   - 5 or more 5xx responses from the Api in 5 minutes;
   - an error logged by the Api; an error logged by the Worker;
   - less than 2 GiB of free storage on the database.
4. **E-mail, from outside the repository.** The topic gets an e-mail
   subscription when an address is given as CDK context (`alarmEmail`, on
   the command line or in `~/.cdk.json`); the public repository holds no
   address. Without one, the alarms still change state but notify no one.
5. **A dashboard**, `FinanceTracker`: the alarms' state; CloudFront
   requests and 5xx rate; the Api's p95 response time; CPU, memory and
   logged errors per service; database CPU, connections and free storage.
   CloudFront's metrics exist only in us-east-1, with the dimension
   `Region=Global`, so those graphs name both explicitly.
6. **A saved Logs Insights query**, `finance-tracker/errors`, listing the
   latest errors from the Api and the Worker.
7. **All of it in the application stack**, so it comes and goes with the
   system it watches.

## Consequences

**Positive:**

- A broken deploy, a crashing service or an exception reaches the inbox
  within minutes, and the recovery too.
- Errors can be found by field instead of by reading.
- Everything stays inside CloudWatch's always-free allowance: five alarms,
  one dashboard, two custom metrics.

**Negative:**

- **No tracing**: a slow request can't be followed through the Api, the
  database and the Worker. That waits for OpenTelemetry.
- **Error alarms are blunt**: one logged error raises an alarm, which is
  right for a demo with no traffic and would be noisy with real users.
- **The JSON logs are harder to read by eye** in the console; Logs Insights
  is the way to read them.
- **The first deploy may raise "Api unreachable"** before the first task is
  healthy, followed by an "OK" e-mail once it is.
- **The subscription has to be confirmed** by clicking the link in AWS's
  first e-mail; until then nothing is delivered.

## Alternatives Considered

- **OpenTelemetry with the AWS Distro collector and X-Ray** -- traces and
  richer metrics, but more application code and a collector container
  beside every service, on tasks with 512 MB of memory.
- **CloudWatch Container Insights** -- per-container metrics without
  setup, but billed per metric; the services' own CPU and memory metrics
  are free and enough here.
- **Alarms on CloudFront's metrics** -- they exist only in us-east-1, and
  alarms must live in the metric's region; the load balancer's metrics
  cover the same failures from inside the region.
- **The e-mail address in `cdk.json`** -- simplest, but it would publish
  the address in a public repository.
