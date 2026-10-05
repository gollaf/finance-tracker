# Finance Tracker

[![build](https://github.com/gollaf/finance-tracker/actions/workflows/ci.yml/badge.svg)](https://github.com/gollaf/finance-tracker/actions/workflows/ci.yml)
[![license](https://img.shields.io/badge/license-MIT-blue)](./LICENSE)
![.NET](https://img.shields.io/badge/.NET-10-purple)
![PostgreSQL](https://img.shields.io/badge/PostgreSQL-16-blue)
![RabbitMQ](https://img.shields.io/badge/RabbitMQ-4-orange)

A personal finance API: record transactions by hand or by CSV import, have
them categorized by rules or by AI, set monthly budgets, and get
plain-language summaries of where the money went.

It is a portfolio project, built to practise backend architecture and
operations end to end -- from the domain model to messaging, containers,
Kubernetes and CI/CD. Every significant decision is written up as an
[Architecture Decision Record](./docs/adr).

## Highlights

- **Clean Architecture with CQRS** -- Domain, Application, Infrastructure,
  Api and Worker projects with the dependency rule pointing inward. Use
  cases are MediatR commands and queries; input is validated once, in a
  [pipeline behavior](./src/FinanceTracker.Application/Common/ValidationBehavior.cs),
  and failures are returned as `Result` values that map to HTTP status codes
  in [one place](./src/FinanceTracker.Api/Common/ResultExtensions.cs).
- **Reliable asynchronous processing** -- a
  [transactional outbox](./docs/adr/0013-transactional-outbox.md) stores each
  event in the same database transaction as the change it describes, and a
  relay publishes it to RabbitMQ. Delivery is at-least-once, with retries, a
  dead-letter queue per consumer, and idempotent consumers.
- **AI that never invents numbers** -- spending figures are computed in
  code; the LLM only puts them into words, and the endpoint falls back to a
  templated summary when the AI is unavailable. AI categorization may only
  choose an existing category and never overwrites one the user set.
- **Tests against real infrastructure** -- unit tests for every handler and
  validator, plus integration tests on real PostgreSQL and RabbitMQ
  containers (Testcontainers), including end-to-end tests of the whole
  outbox -> RabbitMQ -> Worker pipeline.
- **Kubernetes** -- the full system runs on a local cluster: migrations as a
  separate Job, the outbox relay as a single-replica Deployment, and
  scalable Worker consumers.
- **CI/CD** -- every pull request is built, tested and has both Docker
  images built and the Kubernetes manifests validated; `master` publishes
  the images to GitHub Container Registry and is protected by required
  checks.

## Architecture

```
API  ──▶  Application  ──▶  Domain
Worker  ──▶  Application  ──▶  Domain
Infrastructure  ──▶  Application  ──▶  Domain
```

- **Domain** -- entities, value objects, business rules. No external dependencies.
- **Application** -- use cases (CQRS via MediatR), validation, and
  interfaces for everything external.
- **Infrastructure** -- EF Core + PostgreSQL, the transactional outbox,
  RabbitMQ publishing and consuming, the Groq AI client.
- **Api** -- ASP.NET Core Web API.
- **Worker** -- a second process with no HTTP: relays outbox events to
  RabbitMQ and consumes them (AI categorization, background CSV import).

```
API ── one DB commit: change + outbox row ──▶ Postgres ◀── OutboxRelay (Worker)
                                                              │ publish
                                                              ▼
                                                   RabbitMQ ──▶ Worker consumers
```

## Tech Stack

| Area | Technologies |
|---|---|
| Backend | .NET 10, ASP.NET Core, MediatR, FluentValidation |
| Data | PostgreSQL 16, EF Core |
| Messaging | RabbitMQ 4 (raw `RabbitMQ.Client`), transactional outbox |
| AI | Groq free-tier LLM API (OpenAI-compatible) |
| Testing | xUnit, FluentAssertions, NSubstitute, Testcontainers |
| Infrastructure | Docker, docker compose, Kubernetes (Minikube) with Kustomize |
| CI/CD | GitHub Actions, GitHub Container Registry |

## Getting Started

Requires Docker.

```bash
git clone https://github.com/gollaf/finance-tracker.git
cd finance-tracker
docker compose up
```

This starts PostgreSQL, RabbitMQ, the Api and the Worker. Then open:

- Interactive API docs (Scalar): `http://localhost:5000/scalar/v1`
- Health endpoints: `http://localhost:5000/health/live` and `/health/ready`
- RabbitMQ management UI: `http://localhost:15672` (user and password
  `financetracker`, local development only)

**Optional: real AI.** Without an API key everything still works -- AI
summaries fall back to a templated text and transactions simply stay
uncategorized. To enable it, get a free key at console.groq.com (no credit
card) and put it in a `.env` file in the repository root, which is
gitignored:

```
GROQ_API_KEY=gsk_...
```

For `dotnet run` outside Docker, use User Secrets instead:
`dotnet user-secrets set "Groq:ApiKey" "gsk_..."` (from `src/FinanceTracker.Api`).

## API Overview

| Method | Route | Purpose |
|---|---|---|
| POST | `/api/accounts` | Create an account |
| GET | `/api/accounts/{id}/balance` | Balance, computed from its transactions |
| POST | `/api/categories` | Create a category |
| POST | `/api/categorization-rules` | Create a rule that categorizes by description |
| POST | `/api/transactions` | Add a transaction |
| PUT | `/api/transactions/{id}` | Update a transaction |
| DELETE | `/api/transactions/{id}` | Delete a transaction |
| PUT | `/api/transactions/{id}/category` | Set or clear its category |
| GET | `/api/transactions?accountId=&from=&to=` | List an account's transactions |
| GET | `/api/transactions/spending-summary?accountId=&year=&month=` | Spending by category for a month |
| GET | `/api/transactions/spending-insights?accountId=&year=&month=` | AI summary of this month vs the 3-month average |
| POST | `/api/transactions/import` | Start a CSV import (multipart: `AccountId`, `File`) -- returns `202 Accepted` |
| GET | `/api/imports/{id}` | Import job status and rejected rows |
| POST | `/api/budgets` | Create a monthly budget for a category |
| PUT | `/api/budgets/{id}` | Change a budget's limit |
| GET | `/api/budgets/{id}/status` | Spending against the budget |

Every new transaction publishes a `TransactionAdded` event; if no rule
matched it, the Worker asks the AI to pick one of your existing categories
([ADR 0014](./docs/adr/0014-ai-transaction-categorization.md)). A CSV import
runs in the Worker in a single database transaction, so a crash never leaves
a file half-imported or a row imported twice
([ADR 0016](./docs/adr/0016-asynchronous-csv-import.md)).

## Running Tests

```bash
dotnet test
```

Integration tests start real PostgreSQL and RabbitMQ containers through
Testcontainers, so Docker must be running. Only the AI is replaced by a stub;
the tests that call the real Groq API run only when `GROQ_API_KEY` is set.

## Continuous Integration and Delivery

Every pull request and every push to `master` runs
[`.github/workflows/ci.yml`](./.github/workflows/ci.yml)
([ADR 0022](./docs/adr/0022-ci-pipeline-and-image-publishing.md)):

```
pull request / push to master
  ├─ build-and-test ── restore, build, every test suite, results as a check
  │     └─ docker-image (api), docker-image (worker)
  │            build both images; on master also push them to ghcr.io
  └─ k8s-manifests ── kustomize build deploy/k8s, validated by kubeconform
```

All four checks must pass before a pull request can be merged. Images
published from `master` are public:

```bash
docker pull ghcr.io/gollaf/finance-tracker-api:latest           # newest master build
docker pull ghcr.io/gollaf/finance-tracker-worker:sha-4fc8fb3   # one exact commit
```

## Running on Kubernetes (local)

The same system runs on a local [Minikube](https://minikube.sigs.k8s.io/)
cluster from plain manifests in [`deploy/k8s/`](./deploy/k8s), combined with
Kustomize ([ADRs 0017-0021](./docs/adr)).

```
namespace finance-tracker
  api (Deployment, 2 Pods) ── Service api ◀── kubectl port-forward (localhost:5001)
  migrate (Job) ── applies EF Core migrations once per deploy, then exits
  outbox-relay (Deployment, exactly 1)    ┐ same Worker image,
  worker (Deployment, scalable)           ┘ different role (ADR 0018)
  postgres, rabbitmq (StatefulSets, each with its own PersistentVolumeClaim)
```

<details>
<summary>Setup and commands (PowerShell)</summary>

**One-time setup** (Docker Desktop running):

```powershell
winget install Kubernetes.minikube
winget install -e --id Kubernetes.kubectl
minikube start --driver=docker --cpus=2 --memory=4096

# Local credentials for the cluster's Secret -- gitignored, never committed.
Copy-Item deploy/k8s/secrets.env.example deploy/k8s/secrets.env
# then edit deploy/k8s/secrets.env and set every value
```

**Build the images into the cluster and deploy:**

```powershell
docker build -t finance-tracker-api:dev -f src/FinanceTracker.Api/Dockerfile .
docker build -t finance-tracker-worker:dev -f src/FinanceTracker.Worker/Dockerfile .
minikube image load finance-tracker-api:dev
minikube image load finance-tracker-worker:dev

kubectl apply -k deploy/k8s
kubectl get pods -n finance-tracker -w      # wait until everything is Running / Completed
```

**Use it** (each `port-forward` keeps running in its own window):

```powershell
kubectl port-forward svc/api 5001:8080 -n finance-tracker            # API: http://localhost:5001/scalar/v1
kubectl port-forward svc/rabbitmq 15673:15672 -n finance-tracker     # RabbitMQ UI: http://localhost:15673
kubectl port-forward svc/postgres 5433:5432 -n finance-tracker       # Postgres: localhost:5433
kubectl scale deployment worker --replicas=3 -n finance-tracker      # more consumers
```

**After a code change**, rebuild and load the image as above, then
`kubectl rollout restart deployment/api -n finance-tracker` (or
`deployment/worker`, `deployment/outbox-relay`). The tag stays `:dev`, so
without the restart the old code keeps running. If the change adds a
migration, first run `kubectl delete job migrate -n finance-tracker
--ignore-not-found` and `kubectl apply -k deploy/k8s` again.

**Stop / clean up:** `minikube stop` pauses everything and keeps the data;
`minikube delete` removes the cluster and its data.

</details>

## Roadmap

Done: domain and use cases, persistence and API, Docker, AI insights,
asynchronous processing with RabbitMQ, Kubernetes, and CI/CD. Next:

- **Cloud deployment**
  - **Oracle Cloud Always Free VM** -- the permanent, always-on public demo.
  - **Azure sprint** (timeboxed, within the free trial credit) -- Azure
    Container Apps, Application Insights via OpenTelemetry, infrastructure
    as code with Bicep, and passwordless deployment from GitHub Actions.
  - **AWS sprint** (timeboxed, on the free plan) -- the same system on AWS's
    managed container services, to compare the two platforms.
- **Authentication** -- required before the API is exposed publicly.
- **Angular frontend.**

The phase-by-phase plan is in [`PROJECT_PLAN.md`](./PROJECT_PLAN.md).

## Known Limitations

- **No authentication yet.** Every endpoint is open; this is fine on a local
  machine and is the first thing to add before a public deployment.
- **Local-development configuration.** docker compose and the Kubernetes
  manifests run in the Development environment (API docs and exception
  details enabled) with local-only credentials.
- **One outbox relay at a time.** This is enforced by deployment
  configuration, not code; running several would need row locking
  (`FOR UPDATE SKIP LOCKED`).

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
- [0017 — Database migrations as a separate deployment step](./docs/adr/0017-migrations-as-a-separate-deployment-step.md)
- [0018 — Worker roles: outbox relay and consumers as separately deployable parts](./docs/adr/0018-worker-roles.md)
- [0019 — Local Kubernetes cluster and manifest layout](./docs/adr/0019-local-kubernetes-cluster-and-manifests.md)
- [0020 — Application images and rollout in the local cluster](./docs/adr/0020-application-images-and-rollout-in-the-local-cluster.md)
- [0021 — Worker in Kubernetes: no health probes, explicit shutdown window](./docs/adr/0021-worker-probes-and-shutdown-in-kubernetes.md)
- [0022 — CI pipeline and container image publishing](./docs/adr/0022-ci-pipeline-and-image-publishing.md)

## License

[MIT](./LICENSE) © 2026 Ihar Dziamidka
