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
Infrastructure  ──▶  Application  ──▶  Domain
```

- **Domain** — entities, value objects, business rules. No external dependencies.
- **Application** — use cases (CQRS via MediatR), validation, interfaces for
  everything external.
- **Infrastructure** — EF Core + PostgreSQL, RabbitMQ publisher, AI client.
- **API** — ASP.NET Core Web API, auth, DI composition root.
- **Worker** — background service consuming RabbitMQ for async AI categorization.

## Tech Stack

Backend: .NET 10 · EF Core · MediatR · FluentValidation
Data: PostgreSQL
Messaging: RabbitMQ (MassTransit)
AI: Ollama / free-tier LLM API
Testing: xUnit · FluentAssertions · NSubstitute · Testcontainers
Infra: Docker · Kubernetes · GitHub Actions
Frontend (planned): Angular

## Getting Started

```bash
git clone https://github.com/gollaf/finance-tracker.git
cd finance-tracker
docker compose up
```

This starts the API, PostgreSQL, RabbitMQ, and the Worker service.
API docs available at `http://localhost:5000/swagger` once running.

## Running Tests

```bash
dotnet test
```

Integration tests spin up a real PostgreSQL instance via Testcontainers —
Docker must be running.

## Roadmap

See [`PROJECT_PLAN.md`](./PROJECT_PLAN.md) for the full phase-by-phase plan
and current progress.

## Architecture Decisions

Significant decisions are logged as ADRs in [`docs/adr/`](./docs/adr):

- [0001 — PostgreSQL over MSSQL](./docs/adr/0001-postgresql-over-mssql.md)

## License

MIT
