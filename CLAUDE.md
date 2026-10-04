# CLAUDE.md

Guidance for Claude Code (or any AI coding assistant) working in this repo.

## Project

Personal finance tracker — .NET 10, Clean Architecture, CQRS via MediatR,
PostgreSQL, RabbitMQ, Ollama/Groq for AI features. Full plan and roadmap:
`PROJECT_PLAN.md`. Architecture decisions and their reasoning: `docs/adr/`.

## Conventions

- Commit messages: Conventional Commits — see `CONTRIBUTING.md`
- Branching: GitHub Flow — feature branches off `master`, PR to merge, no
  direct commits to `master` except the initial scaffold
- Every command/query handler ships with a unit test in the same commit,
  not added later
- New architectural decisions get an ADR in `docs/adr/`, not just a comment
- Code comments never reference project phases (e.g. "Phase 1", "Phase 4")
  — a comment explains the code's own reasoning, not where it sits on the
  roadmap; phase context belongs in `PROJECT_PLAN.md` only
- An AI assistant working in this repo never runs git commands itself —
  it supplies branch names, commit messages, and PR text; the human runs
  every `git` command by hand

## Commands

- Build: `dotnet build`
- Test: `dotnet test`
- Run locally (API + Postgres + RabbitMQ + Worker): `docker compose up`
- Run on the local Kubernetes cluster (Minikube): see README's "Running on
  Kubernetes (local)" -- manifests in `deploy/k8s/`, applied with
  `kubectl apply -k deploy/k8s`; `deploy/k8s/secrets.env` is gitignored and
  must never be committed
- CI: `.github/workflows/ci.yml` (ADR 0022). Its jobs `build-and-test`,
  `docker-image (api)`, `docker-image (worker)` and `k8s-manifests` are
  required checks for merging into `master` -- renaming any of them also
  means updating the repository ruleset, or the old name blocks every PR.
  Images are pushed to `ghcr.io/gollaf/finance-tracker-{api,worker}` from
  `master` only

## Constraints

- No paid subscriptions — free tiers and local tooling only (Ollama,
  Testcontainers, Minikube, GitHub Actions free minutes)
- Domain layer has zero external package references — if a suggested
  change needs one there, it belongs in a different layer instead
