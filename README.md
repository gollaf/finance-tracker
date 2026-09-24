# Finance Tracker

[![build](https://github.com/gollaf/finance-tracker/actions/workflows/ci.yml/badge.svg)](https://github.com/gollaf/finance-tracker/actions/workflows/ci.yml)
![license](https://img.shields.io/badge/license-MIT-blue)
![.NET](https://img.shields.io/badge/.NET-10-purple)
![PostgreSQL](https://img.shields.io/badge/PostgreSQL-16-blue)

> 🚧 Personal portfolio project, under active development. See
> [`PROJECT_PLAN.md`](./PROJECT_PLAN.md) for the full roadmap.

A personal finance tracker with AI-powered spending insights — add
transactions, get them auto-categorized, set budgets, and get plain-language
summaries of your spending instead of just raw charts.

## Why This Project

Built to use Clean Architecture, CQRS, Docker, Kubernetes, RabbitMQ, and
AI integration hands-on, coming from a .NET/Angular background. Every
architectural decision is written up in [`docs/adr/`](./docs/adr).

## Architecture

Clean Architecture with the dependency rule pointing inward:

```
API  ──▶  Application  ──▶  Domain
Worker  ──▶  Application  ──▶  Domain
Infrastructure  ──▶  Application  ──▶  Domain
```

- **Domain** — entities, value objects, business rules. No external dependencies.
- **Application** — use cases (CQRS via MediatR), validation, interfaces for
  everything external.
- **Infrastructure** — EF Core + PostgreSQL, the transactional outbox,
  RabbitMQ publisher/consumer plumbing, the Groq AI client.
- **API** — ASP.NET Core Web API, DI composition root.
- **Worker** — a second composition root with no HTTP: relays outbox events
  to RabbitMQ and consumes them — AI categorization of new transactions and
  background CSV import.

## Tech Stack

Backend: .NET 10 · EF Core · MediatR · FluentValidation
Data: PostgreSQL
Messaging: RabbitMQ (raw RabbitMQ.Client) · transactional outbox
AI: Groq free-tier LLM API
Testing: xUnit · FluentAssertions · NSubstitute · Testcontainers
Infra: Docker · Kubernetes · GitHub Actions
Frontend (planned): Angular

## Getting Started

```bash
git clone https://github.com/gollaf/finance-tracker.git
cd finance-tracker
docker compose up
```

This starts PostgreSQL, RabbitMQ, the API, and the Worker. Once running:

- Interactive API docs (Scalar, Development only): `http://localhost:5000/scalar/v1`
- Liveness/readiness health endpoints: `/health/live` and `/health/ready`
- RabbitMQ management UI: `http://localhost:15672` (user and password
  `financetracker`, local development only)

## Running Tests

```bash
dotnet test
```

Integration tests spin up real PostgreSQL and RabbitMQ instances via
Testcontainers — Docker must be running. `FinanceTracker.Worker.IntegrationTests`
runs the whole asynchronous pipeline end to end, with only the AI replaced
by a stub.

## AI-Powered Spending Insights

`GET /api/transactions/spending-insights?accountId=...&year=...&month=...`
computes this-month-vs-prior-3-month-average spending per category, then
asks an LLM (Groq's free-tier API, OpenAI-compatible) to describe the
numbers in plain language. The AI only ever rewords numbers the API already
computed — it never invents its own — and a failed or unconfigured AI call
degrades gracefully to a templated narrative instead of failing the
request. See [ADR 0010](./docs/adr/0010-ai-insights-provider-and-integration-design.md)
for the full design rationale.

To enable real AI narratives locally, set a free Groq API key
(console.groq.com, no credit card required):

```bash
# dotnet run (from src/FinanceTracker.Api)
dotnet user-secrets set "Groq:ApiKey" "gsk_..."

# docker compose up — put this in a gitignored .env file at the repo root
GROQ_API_KEY=gsk_...
```

Without a key, the endpoint still works — `narrativeGeneratedByAi` is
`false` and `narrative` is a templated fallback built from the same
per-category numbers.

## Asynchronous Processing

Work that doesn't need to finish inside an HTTP request runs in the Worker,
driven by RabbitMQ ([ADR 0011](./docs/adr/0011-async-messaging-rabbitmq-raw-client.md)):

```
API ── one DB commit: change + outbox row ──▶ Postgres ◀── OutboxRelay (Worker)
                                                              │ publish
                                                              ▼
                                                   RabbitMQ ──▶ Worker consumers
```

- **Transactional outbox** — an event is stored in the same database
  transaction as the change it describes, then relayed to RabbitMQ, so it
  can't be lost between the two ([ADR 0013](./docs/adr/0013-transactional-outbox.md)).
  Delivery is at-least-once with manual acks, retries, and a dead-letter
  queue per consumer, and every consumer is idempotent
  ([ADR 0012](./docs/adr/0012-rabbitmq-topology-and-delivery-guarantees.md)).
- **AI categorization** — every new transaction publishes `TransactionAdded`.
  If no categorization rule matched it, the Worker asks the AI to pick one of
  your existing categories; the AI can't invent categories or overwrite one
  you set yourself ([ADR 0014](./docs/adr/0014-ai-transaction-categorization.md)).
  The Worker uses the same `GROQ_API_KEY` as above; without it, transactions
  simply stay uncategorized.
- **CSV import** — `POST /api/transactions/import` (multipart: `AccountId`,
  `File`) parses the file and answers `202 Accepted` with an import job id
  and a `Location` header. Poll `GET /api/imports/{id}` for the outcome:
  the imported count and every rejected row by line number. The whole
  import runs in one database transaction, so a crash never leaves a file
  half-imported or a row imported twice
  ([ADR 0016](./docs/adr/0016-asynchronous-csv-import.md)).

## Roadmap

See [`PROJECT_PLAN.md`](./PROJECT_PLAN.md) for the full phase-by-phase plan
and current progress.

## Architecture Decisions

Significant decisions are logged as ADRs in [`docs/adr/`](./docs/adr):

- [0001 — PostgreSQL over MSSQL](./docs/adr/0001-postgresql-over-mssql.md)
- [0002 — Transaction as a separate aggregate; Account has no stored balance](./docs/adr/0002-transaction-separate-aggregate-no-stored-balance.md)
- [0003 — EF Core persistence mapping for strongly-typed IDs and value objects](./docs/adr/0003-ef-core-persistence-mapping.md)
- [0004 — API layer: MVC controllers and a fixed Result-to-HTTP mapping](./docs/adr/0004-api-mvc-controllers-result-mapping.md)
- [0005 — Cross-aggregate foreign keys without navigation properties](./docs/adr/0005-cross-aggregate-foreign-keys.md)
- [0006 — CSV import parsing lives in the API layer](./docs/adr/0006-csv-import-parsing-in-api-layer.md)
- [0007 — Dockerfile layout, multi-stage build, and base images](./docs/adr/0007-dockerfile-multistage-build-and-base-images.md)
- [0008 — docker-compose topology and startup migrations](./docs/adr/0008-compose-topology-and-startup-migrations.md)
- [0009 — Separate liveness and readiness health endpoints](./docs/adr/0009-liveness-and-readiness-health-endpoints.md)
- [0010 — AI insights provider and integration design](./docs/adr/0010-ai-insights-provider-and-integration-design.md)
- [0011 — Asynchronous messaging: RabbitMQ with the raw RabbitMQ.Client library](./docs/adr/0011-async-messaging-rabbitmq-raw-client.md)
- [0012 — RabbitMQ topology and delivery guarantees](./docs/adr/0012-rabbitmq-topology-and-delivery-guarantees.md)
- [0013 — Transactional outbox](./docs/adr/0013-transactional-outbox.md)
- [0014 — AI categorization of uncategorized transactions](./docs/adr/0014-ai-transaction-categorization.md)
- [0015 — Integration event contracts](./docs/adr/0015-integration-event-contracts.md)
- [0016 — Asynchronous CSV import](./docs/adr/0016-asynchronous-csv-import.md)

## License

MIT
