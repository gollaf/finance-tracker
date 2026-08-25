# Contributing Guide

This is a solo learning/portfolio project, but it follows the same workflow
discipline as a team project — partly because it's good practice, partly
because a clean history is part of what this project demonstrates.

## Branching Strategy — GitHub Flow

No `develop`/`release` branches (that's GitFlow, built for teams shipping
parallel releases — overkill here).

- `master` is always in a working, buildable state.
- No direct commits to `master`, except the initial scaffold.
- Every unit of work gets its own branch off `master`.
- Open a PR, self-review it, merge, delete the branch.

**Branch naming** — `<type>/<kebab-case-description>`:

```
feat/add-transaction-command
test/account-invariants
fix/csv-import-null-category
docs/adr-0002-rabbitmq
chore/ci-setup
refactor/repository-interfaces
```

## Commit Messages — Conventional Commits

```
<type>(<scope>): <summary, imperative mood, no period, ≤50 chars>

<optional body — explain WHY, not what, wrapped ~72 chars>

<optional footer — Closes #12>
```

| Type | Use for |
|---|---|
| `feat` | a new feature or use case |
| `fix` | a bug fix |
| `test` | adding/adjusting tests only |
| `docs` | documentation only |
| `refactor` | code change, no behavior change |
| `chore` | tooling, config, dependencies |
| `ci` | CI/CD pipeline changes |
| `perf` | performance improvement |

Examples:

```
feat(application): add AddTransaction command and handler
test(domain): cover Account balance invariants
ci: add GitHub Actions workflow for build and test
docs(adr): record decision to use PostgreSQL over MSSQL
fix(worker): retry RabbitMQ connection on startup
```

## Rules Per Commit

1. One logical change per commit — don't mix a feature with unrelated cleanup.
2. Imperative mood, ≤50 char summary, no trailing period.
3. Commit tests with the code they test, not batched at the end.
4. Every commit leaves the solution buildable. Unfinished work stays on
   its branch, never pushed broken to `master`.
5. No direct commits to `master` except the initial scaffold commit.

## Before Merging a PR

- [ ] `dotnet build` succeeds
- [ ] `dotnet test` passes
- [ ] Commit messages follow the convention above
- [ ] No unrelated changes mixed into the branch
