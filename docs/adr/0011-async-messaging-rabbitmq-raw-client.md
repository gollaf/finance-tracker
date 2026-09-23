# 11. Asynchronous messaging: RabbitMQ with the raw RabbitMQ.Client library

## Status

Accepted

## Context

Phase 5 of `PROJECT_PLAN.md` moves work that doesn't need to finish inside
an HTTP request onto a message broker, processed by a separate Worker
process. Three coupled questions have to be answered before any messaging
code is written:

1. Which work actually moves off the request path -- and which doesn't.
2. Which .NET library talks to the broker.
3. How the broker itself runs locally under `docker compose up`.

Two facts about the current codebase shape the first question:

- The only AI feature today is `GetSpendingInsights` (ADR 0010). There is
  no AI-assisted categorization: `TransactionCategorizer` is rules-only,
  and a Transaction no rule matches simply stays uncategorized.
- `ImportTransactionsFromCsvCommandHandler` processes every row inside the
  upload request, so a large bank statement holds the HTTP connection open
  for as long as the whole import takes.

## Decision

### 1. What goes async, and what deliberately stays synchronous

- **AI categorization of uncategorized Transactions -- new, async.** When a
  Transaction is added (manually or by import) and no CategorizationRule
  matches it, an event is published; the Worker asks the AI to pick one of
  the *existing* Categories. The user doesn't need that answer in the
  response that created the Transaction -- "saved now, category appears a
  moment later" is an acceptable, even expected, experience. This is the
  textbook fit for messaging: fire-and-forget, eventually consistent, and
  the AI's latency and rate limits never touch the request path.
- **CSV import -- moved to async.** The upload endpoint validates the
  file, records an import job, and returns `202 Accepted` with the job's
  id; the Worker performs the import, and a status endpoint reports
  progress and per-row errors.
- **`GetSpendingInsights` -- stays synchronous.** It is a read, and ADR
  0010 already guarantees it can't fail because of the AI (templated
  fallback). Making it async would mean a POST to start a job, a stored
  job, and a GET to poll for the result -- three new moving parts to avoid
  a roughly one-second, already-bounded wait. That trade isn't worth it
  here; this ADR records the choice so it doesn't read as an omission.

### 2. Library: the raw `RabbitMQ.Client`, not a messaging framework

The broker is RabbitMQ, as already planned. The client library is the
official `RabbitMQ.Client` package (7.x, the async-first API built around
`IChannel`), dual-licensed Apache 2.0 / MPL 2.0 -- free, with no
commercial tier.

The project's primary goal is learning, and a framework would hide exactly
the parts worth learning: exchanges and queues, bindings, manual
acknowledgements, redelivery, dead-lettering, and why "exactly-once
delivery" isn't something a broker can actually give you. With the raw
client, each of those becomes code this project writes and tests itself.

### 3. The broker stays behind Application-layer ports

Application never references `RabbitMQ.Client`. It gets its own small
interfaces for "record that this event happened" (introduced alongside the
transactional outbox, in its own ADR); Infrastructure implements them with
RabbitMQ. This is the same rule every repository and `IInsightsGenerator`
already follow -- and it means swapping to a framework later (see
Alternatives) touches Infrastructure and the Worker host only.

### 4. Local broker in `docker-compose.yml`

- Image `rabbitmq:4-management`: RabbitMQ 4.x plus the management plugin,
  which serves a browser UI at `http://localhost:15672` for inspecting
  exchanges, queues, and messages by hand. Pinned to a major version, like
  `postgres:16`.
- Explicit credentials (`RABBITMQ_DEFAULT_USER`/`RABBITMQ_DEFAULT_PASS`)
  rather than the built-in `guest` user. RabbitMQ itself only allows
  `guest` to connect from localhost; the official Docker image happens to
  relax that, but configuration that only works because of one image's
  default would break on any other broker (a Kubernetes deployment, a
  hosted instance). The credentials are committed in plaintext for the
  same reason ADR 0008 gives for the Postgres ones: the broker is only
  reachable on a developer's own machine.
- A fixed `hostname: rabbitmq`. RabbitMQ stores its data under a
  directory named after its node name, which is derived from the
  container's hostname. Without a fixed hostname, compose gives every
  recreated container a new random one, so the node would start with an
  empty data directory every time, and anything in the named volume would
  be silently ignored.
- A healthcheck (`rabbitmq-diagnostics -q check_port_connectivity`), so
  services that depend on the broker can wait for `service_healthy`,
  exactly like `api` already waits on `postgres` (ADR 0008). The lighter
  `ping` check was not used: it only proves the node's runtime responds,
  which can happen before the AMQP listener clients connect to is open.
  A healthcheck only helps at startup, though -- if the broker restarts
  later, the application's own reconnect logic has to cope.
- Host ports `5672` (AMQP, so a locally `dotnet run` API or Worker can
  connect) and `15672` (management UI).

## Consequences

**Positive:**

- Every messaging concept used in this project is visible in its own
  code and tests, not configured away inside a framework.
- No licensing risk: nothing here has a commercial tier to accidentally
  cross into (see Alternatives for why that isn't hypothetical).
- AI categorization's latency, rate limits, and outages can never slow
  down or fail the request that created a Transaction.
- Swapping to a framework later is an Infrastructure/Worker change only.

**Negative:**

- This project owns everything a framework would have provided: message
  serialization and envelopes, connection and channel lifecycle, retry and
  dead-letter topology, consumer error handling, idempotency, and the
  transactional outbox. Each gets its own piece and, where it's a real
  decision, its own ADR -- noticeably more code than the framework path.
- A second process (the Worker) and a third container (RabbitMQ) join
  the local stack, and the system becomes eventually consistent: a
  just-added Transaction can briefly read as uncategorized.
- CSV import's API contract changes from "the response contains the
  result" to "the response contains a job id to poll" -- a breaking
  change for any client of the current endpoint.

## Alternatives Considered

- **MassTransit** -- the tech-stack table's original choice, and the most
  common .NET messaging framework. Rejected for two reasons. From v9 on it
  is commercially licensed; v8 remains open-source but is the end of that
  line, so choosing it means pinning to a version with no open-source
  future, the same situation as this project's `MediatR` pin at 12.4.1.
  And the learning goal above: it would hide the primitives this phase
  exists to teach.
- **Wolverine** -- MIT-licensed, modern, has its own outbox. Rejected for
  the same "hides the primitives" reason, and because its own
  command/handler model overlaps with the MediatR pipeline this project
  already uses.
- **Making `GetSpendingInsights` async as well** -- rejected; see
  decision 1.
- **Running background work in-process (`BackgroundService` inside the
  API, or `System.Threading.Channels`) instead of a broker** -- rejected.
  Queued work would be lost on every API restart, and it couldn't be
  scaled or deployed independently of the API -- which is the point of a
  separate Worker once Kubernetes arrives.
