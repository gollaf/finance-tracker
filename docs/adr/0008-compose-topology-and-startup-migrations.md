# 8. docker-compose topology and startup migrations

## Status

Accepted

## Context

ADR 0007 covers the `Api` image itself. Phase 3 also needs a way to run
that image together with a real PostgreSQL instance with a single command
(`docker compose up`), which raises three questions that only apply once
two containers have to cooperate: how the two containers reach each other
over the network, how `api` avoids starting before `postgres` is actually
ready to accept connections (nothing in the app retries a failed database
connection today), and how the database schema gets created at all, since
nothing outside the test suite currently calls `Database.MigrateAsync()`.

`README.md`'s existing "Getting Started" section already describes
`docker compose up` as starting "the API, PostgreSQL, RabbitMQ, and the
Worker service" -- aspirational text written ahead of the phases that
actually add RabbitMQ and a Worker project (Phase 5/6). This compose file
intentionally covers only what exists today, `api` and `postgres`; the
README correction is tracked separately as this phase's last piece
rather than folded in here.

## Decision

1. **One `docker-compose.yml` at the repo root**, two services:
   `postgres` (the official `postgres:16` image) and `api` (built from
   `src/FinanceTracker.Api/Dockerfile`, per ADR 0007). No explicit
   `networks:` block -- compose creates one default bridge network for
   every file and attaches every service in it automatically, and every
   service can reach every other one there by service name via Docker's
   embedded DNS. `api` connects to `Host=postgres`, never `localhost` --
   `postgres` only resolves *inside* that network, which is exactly why
   the connection string used here differs from the `localhost`-based one
   `FinanceTrackerDbContextFactory` uses for local `dotnet ef` commands
   run directly on the host.
2. **`postgres` gets a `healthcheck` (`pg_isready`), and `api` declares
   `depends_on: postgres: condition: service_healthy`.** Plain
   `depends_on` (no condition) only waits for a container to *exist and
   have started* -- Postgres's own process can take a few seconds after
   that to finish its own startup and actually accept connections. Since
   nothing in `AddInfrastructure`/`Program.cs` retries a failed DB
   connection, `api` starting even slightly too early would crash
   immediately. Gating on the healthcheck instead of adding retry logic
   to the app is the simpler fix for local development; a real
   production deployment (Phase 8) would likely want both.
3. **`postgres` gets a named volume (`postgres-data:/var/lib/postgresql/data`)**,
   not a bind mount to a host folder. A container's filesystem is
   destroyed along with the container; a named volume is what makes the
   database survive `docker compose down` (recreating the containers)
   while still being fully wiped by `docker compose down -v` when a clean
   slate is wanted. Docker manages where it actually lives, which is the
   right default here -- nothing in this project needs to read Postgres's
   raw data files directly from the host.
4. **The connection string reaches `api` via the `ConnectionStrings__FinanceTracker`
   environment variable**, set directly in the compose file (this is a
   local-only, non-secret default -- `postgres`/`postgres` -- not a
   production credential). ASP.NET Core's configuration system maps a
   double-underscore-delimited environment variable to the equivalent
   nested configuration key, so this needs no code change to be picked
   up by the same `configuration.GetConnectionString("FinanceTracker")`
   call `AddInfrastructure` already had.
5. **`Program.cs` now applies any pending EF Core migration on every
   startup**, via `Database.MigrateAsync()` immediately after
   `builder.Build()`. `Database.MigrateAsync()` is idempotent -- it only
   applies migrations not already recorded as applied -- so this is safe
   to run unconditionally on every boot, including the
   `CustomWebApplicationFactory`-driven integration tests, which already
   call `MigrateAsync()` themselves before any test runs; the second call
   there is now a fast no-op, not a conflict.

## Consequences

**Positive:**

- `docker compose up` alone produces a fully working, fully migrated API
  reachable at `http://localhost:5000`, with no manual `dotnet ef
  database update` step.
- The healthcheck gate is a small, declarative fix for a real startup-race
  problem, without adding retry/backoff logic to the application itself.
- A named volume gives real persistence across ordinary restarts while
  keeping a one-command path (`down -v`) back to a clean slate --
  valuable while still learning and expecting to break things.

**Negative:**

- **Auto-migrating on every startup does not scale to multiple concurrent
  instances of this API.** If Phase 6 ever runs more than one replica of
  `api` (a realistic Kubernetes scenario), every replica would race to
  apply the same migration on boot. EF Core's migration history table
  guards against literally corrupting data, but concurrent schema changes
  from multiple instances are still a real hazard to design around
  properly at that point -- most likely by moving migration to a
  dedicated one-shot step (a Kubernetes `Job`/init container) instead of
  in-process. Acceptable to defer until that phase actually introduces
  multiple replicas; not worth the added complexity for a single-instance
  local compose setup today.
- The Postgres credentials in this file are plaintext and committed to
  git. Acceptable because they're meaningless outside this local compose
  network (nothing external can reach `postgres` on it), but this is not
  a pattern to repeat once real secrets are involved -- Phase 8's actual
  deployment will need a real secrets story (environment-specific
  secrets, not committed defaults).
- Mapping `postgres`'s port to the host (`5432:5432`) will conflict if
  another local Postgres (e.g. one already used for `dotnet run`/`dotnet
  ef` outside Docker) is bound to the same host port. Documented as a
  known rough edge rather than solved here, since the fix (stop the other
  instance, or remap to e.g. `5433:5432`) is situational.

## Alternatives Considered

- **A depends_on with no health condition, plus a retry policy (e.g.
  Polly) around the app's own DB connection** -- more robust, and likely
  the right call for a real production deployment, but more machinery
  than local `docker compose up` needs right now. Revisit if Phase 8's
  deployment work wants it.
- **A separate one-shot "migrator" service/container in the compose file**,
  run once before `api` starts, instead of migrating in `Program.cs` --
  more correct for a multi-replica future, but premature while there is
  only ever one `api` instance. Noted above as the likely direction once
  Phase 6 actually needs it, rather than built speculatively now.
- **A bind-mounted host folder for Postgres's data directory** instead of
  a named volume -- rejected; nothing in this project needs to inspect
  Postgres's raw files from the host, and named volumes avoid Postgres's
  known permission friction with bind mounts across host operating
  systems.
