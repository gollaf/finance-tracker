# Finance Tracker — Project Plan

## What This Is

A personal finance tracker, similar in spirit to Mint or YNAB but self-built
and intentionally scoped to teach specific technologies.

- Add transactions manually or via bulk CSV import
- Transactions get auto-categorized (rules + AI for ambiguous cases)
- Set monthly budgets per category, track actual vs. budget
- Dashboard: balances, recent transactions, budget status
- AI-generated plain-language spending insights ("You spent 28% more on
  dining out this month than your 3-month average")

## Core Use Cases

**Commands:** CreateAccount, AddTransaction, UpdateTransaction,
DeleteTransaction, CreateBudget, UpdateBudget, CategorizeTransaction,
ImportTransactionsFromCsv

**Queries:** GetTransactions (filtered/paged), GetSpendingSummary,
GetBudgetStatus, GetSpendingInsights, GetAccountBalance

## Tech Stack

| Concern | Choice | Why |
|---|---|---|
| Backend | .NET 10, Clean Architecture, CQRS (MediatR) | Industry-standard, testable |
| Database | PostgreSQL + EF Core | See `docs/adr/0001-postgresql-over-mssql.md` |
| Messaging | RabbitMQ + MassTransit | Decouple AI calls from request path |
| AI | Ollama (local) / Groq free tier | No subscription cost |
| Testing | xUnit, FluentAssertions, NSubstitute, Testcontainers | |
| Containers | Docker, Kubernetes (Minikube/Kind locally) | |
| Deployment | Oracle Cloud free VM (persistent), AWS (timeboxed learning sprint) | |
| CI/CD | GitHub Actions | |
| Frontend | Angular (later phase) | |

## Architecture

Clean Architecture, dependency rule points inward:

`API → Application → Domain` and `Infrastructure → Application → Domain`

- **Domain** — entities, value objects, business rules. No dependencies.
- **Application** — use cases (commands/queries via MediatR), interfaces for
  everything external (repositories, AI service, event publisher).
- **Infrastructure** — EF Core/Postgres, RabbitMQ publisher, AI client.
  Implements Application's interfaces.
- **API** — controllers, auth, wires everything via DI.

## Roadmap

- [ ] **Phase 1** — Domain & Application core (entities, use cases, unit tests)
- [ ] **Phase 2** — Infrastructure & API (EF Core, Postgres, Testcontainers)
- [ ] **Phase 3** — Dockerize (Dockerfile, docker-compose)
- [ ] **Phase 4** — AI feature, synchronous first version
- [ ] **Phase 5** — Async processing via RabbitMQ (refactor AI + CSV import)
- [ ] **Phase 6** — Kubernetes locally (multi-service: API, Worker, RabbitMQ, Postgres)
- [ ] **Phase 7** — CI/CD via GitHub Actions
- [ ] **Phase 8** — Deploy: Oracle free VM (persistent), AWS sprint (timeboxed)
- [ ] **Phase 9** — Angular frontend

## Constraints

- No paid subscriptions — free tiers / local tooling only
- Every use case gets a unit test before moving on
- Each significant architectural choice gets an ADR in `docs/adr/`
