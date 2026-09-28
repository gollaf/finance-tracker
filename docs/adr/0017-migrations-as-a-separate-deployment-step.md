# 17. Database migrations as a separate deployment step

## Status

Accepted. Amends ADR 0008 (startup migrations) for deployments that run
the Api with more than one instance, such as Kubernetes.

## Context

ADR 0008 made the Api apply pending EF Core migrations every time it
starts, and flagged that this "does not scale to multiple concurrent
instances": with two replicas of the Api, both would try to migrate the
same database at the same moment.

Re-checking that concern against the versions this project actually
uses: since EF Core 9, `Migrate`/`MigrateAsync` takes a database-wide lock
before applying anything, and the Npgsql provider (10.0.3 here)
implements it as `LOCK TABLE "__EFMigrationsHistory" IN ACCESS EXCLUSIVE
MODE` inside the migration transaction. So two instances migrating at once
do not apply the same migration twice -- the second one waits, then finds
nothing left to do. The data-safety half of ADR 0008's concern is already
handled by the framework.

Migrating inside every instance's startup is still the wrong shape for a
multi-instance deployment, for other reasons:

- **A broken migration takes every instance down.** Each replica runs it,
  each fails, and each ends up in a crash-restart loop. The error is spread
  across N restarting instances' logs instead of one failed step.
- **A slow migration fights the health probes.** An orchestrator gives a
  starting instance a limited time before it considers it failed; a
  migration that rebuilds a large table can outlast that window and get
  the instance killed halfway through.
- **It runs N times on every rollout**, each time taking the lock, when
  once per deployment is enough.
- **It mixes two privileges.** The process serving HTTP traffic needs its
  database user to be allowed to change the schema. A separate step could
  later run with a different, more privileged user than the Api itself.

## Decision

1. **A `migrate` command on the Api.** `dotnet FinanceTracker.Api.dll
   migrate` builds the normal host (same configuration, same connection
   string, same logging), applies pending migrations and exits with code 0,
   without starting the web server. A failure throws and exits non-zero, so
   whatever ran it can see it failed and retry. It is a bare verb, not a
   `--flag`: .NET's command-line configuration provider ignores arguments
   without a leading dash, so the verb can't leak into configuration.
   The same Api image serves both purposes -- no third image, no separate
   migration tool to keep in sync.
2. **Startup migration becomes a setting,
   `Database:ApplyMigrationsOnStartup`, default `true`.** `dotnet run`,
   docker compose and the integration tests keep ADR 0008's zero-step
   behavior unchanged. A deployment that runs `migrate` itself sets
   `Database__ApplyMigrationsOnStartup=false` for the serving instances.
3. **Readiness includes the schema.** `/health/ready` gains a second check,
   `PendingMigrationsHealthCheck`, which is unhealthy while any migration
   this build contains is missing from the database. Without it, an
   instance started before the `migrate` step finished would find the
   database reachable, report ready, and receive traffic it can only answer
   with 500s. The check only compares against this build's own
   migrations, so older instances stay ready while a newer version
   migrates ahead of them during a rollout.
4. **In Kubernetes, `migrate` runs as a `Job`** using the Api image with
   `args: ["migrate"]`, once per deployment; the Api `Deployment` sets
   `Database__ApplyMigrationsOnStartup=false`. (The manifests themselves
   are part of the Kubernetes setup, not of this change.)

## Consequences

**Positive:**

- One failed migration is one failed Job with one log to read, not every
  Api instance crash-looping.
- Api instances start fast and can't be killed by probes mid-migration.
- Nothing changes for local development or compose.
- "Unmigrated database" is now visible from outside, as a failing
  readiness endpoint, instead of only as failing requests.

**Negative:**

- Deploying now has an order that matters: the `migrate` step must run
  for new instances to ever become ready. Kubernetes has no built-in
  "run this Job first" for plain manifests, so the deploy steps have to
  wait for the Job explicitly. The readiness check makes getting this
  wrong safe (no traffic reaches an unmigrated instance), not automatic.
- Every readiness probe now queries `__EFMigrationsHistory`. A single
  small-table read every few seconds per instance is negligible here.
- Migrations must stay backward compatible with the previous version of
  the code (for example: add a column in one release, drop the old one in
  a later release), because old and new instances briefly share a
  database during a rollout. This was already true under ADR 0008; it's
  just more visible now.

## Alternatives Considered

- **Keep migrating on startup and rely on EF Core's lock** -- safe from a
  data point of view (see Context), but keeps every other problem listed
  there.
- **An EF Core migrations bundle** (`dotnet ef migrations bundle`), a
  standalone migrator executable and Microsoft's documented option for
  deployments -- rejected for now: it needs a third image or build stage
  with the `dotnet-ef` tool, and a second way of supplying configuration,
  for no benefit over the `migrate` verb in this project.
- **A Kubernetes init container that migrates before each Api pod starts**
  -- rejected: init containers run once per pod, so N replicas still
  migrate N times; it only moves the problem.
- **Generating an idempotent SQL script (`dotnet ef migrations script
  --idempotent`) and applying it with `psql`** -- works, but puts a second
  toolchain in the deploy path, and the script has to be regenerated and
  shipped alongside every image.
