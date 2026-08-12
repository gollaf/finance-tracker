# CLAUDE.md

Guidance for Claude Code (or any AI coding assistant) working in this repo.

## Project

Personal finance tracker — .NET 8, Clean Architecture, CQRS via MediatR,
PostgreSQL, RabbitMQ, Ollama/Groq for AI features. Full plan and roadmap:
`PROJECT_PLAN.md`. Architecture decisions and their reasoning: `docs/adr/`.

## Conventions

- Commit messages: Conventional Commits — see `CONTRIBUTING.md`
- Branching: GitHub Flow — feature branches off `main`, PR to merge, no
  direct commits to `main` except the initial scaffold
- Every command/query handler ships with a unit test in the same commit,
  not added later
- New architectural decisions get an ADR in `docs/adr/`, not just a comment

## Commands

- Build: `dotnet build`
- Test: `dotnet test`
- Run locally (API + Postgres + RabbitMQ + Worker): `docker compose up`

## Constraints

- No paid subscriptions — free tiers and local tooling only (Ollama,
  Testcontainers, Minikube, GitHub Actions free minutes)
- Domain layer has zero external package references — if a suggested
  change needs one there, it belongs in a different layer instead
