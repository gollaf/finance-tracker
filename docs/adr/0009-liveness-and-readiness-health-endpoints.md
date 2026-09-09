# 9. Separate liveness and readiness health endpoints

## Status

Accepted

## Context

Phase 3 wants a way for something outside the process -- right now just a
human running `curl`, but by design also the container orchestrator in
Phase 6 -- to ask "is this instance okay?" ASP.NET Core's built-in health
checks (`AddHealthChecks()`/`MapHealthChecks()`, part of the shared
framework already) make this cheap to add. The only real design question
is what a single health endpoint should actually mean, and the answer
depends on who is asking.

Kubernetes (Phase 6's eventual target) asks this question two different
ways, for two different purposes:

- **Liveness** -- "is this process still functioning, or should you kill
  and restart it?" A liveness probe failing causes a pod restart.
- **Readiness** -- "should traffic currently be routed to this instance?"
  A readiness probe failing just pulls the pod out of the load balancer
  until it passes again -- no restart.

Those are not the same question. If a single `/health` endpoint checked
database connectivity and were wired to both liveness *and* readiness (an
easy mistake to make, since it looks like "one health check" is enough),
a temporary Postgres outage would not just correctly stop traffic from
routing to the API -- it would also make Kubernetes conclude the API
process itself is broken and restart it, repeatedly, for a problem
restarting the API cannot fix. That failure mode -- a dependency outage
turning into a self-inflicted restart loop on the *dependent* service --
is common enough to have a name ("cascading restarts") and is exactly
what liveness/readiness exists to prevent.

## Decision

1. Two endpoints, not one: **`/health/live`** and **`/health/ready`**.
2. `/health/live` runs **zero** registered checks
   (`Predicate = _ => false`), so it can only ever fail if the ASP.NET
   Core pipeline itself cannot process a request at all. It never
   inspects the database.
3. `/health/ready` runs only checks tagged `"ready"` -- today, a single
   `AddDbContextCheck<FinanceTrackerDbContext>` that confirms the
   database is reachable. Anything added later that this instance
   genuinely cannot serve traffic without (another external dependency,
   say) gets the same `"ready"` tag; anything that shouldn't be able to
   trigger a restart never does.
4. Both endpoints exist now, in Phase 3, even though nothing consumes
   them yet -- no Kubernetes probe config exists until Phase 6. Building
   the correct shape now, backed by a real `curl`-able endpoint, costs
   nothing extra and means Phase 6 wires up probes against an
   already-correct design instead of discovering this distinction under
   pressure while also learning Kubernetes for the first time.

## Consequences

**Positive:**

- The restart-loop failure mode described above is structurally
  impossible with this split -- there's no single endpoint that could be
  wired to both probe types by mistake.
- Phase 6's Kubernetes deployment manifest can point `livenessProbe` and
  `readinessProbe` straight at these two paths with zero application
  changes.
- `AddDbContextCheck` reuses the same `FinanceTrackerDbContext` and
  connection string already wired up by `AddInfrastructure` -- no
  parallel configuration to keep in sync.

**Negative:**

- Two endpoints to reason about instead of one, which is genuinely more
  than a single-instance local setup strictly needs today -- this is a
  deliberate bet on Phase 6 needing the distinction, not a requirement
  Phase 3 has on its own.
- `/health/live` returning healthy while the database is down is correct
  Kubernetes-probe behavior, but can look surprising the first time
  someone expects "health" to mean "everything is fine" -- worth
  remembering that `/health/ready` is the one that actually reflects
  dependency state.

## Alternatives Considered

- **A single `/health` endpoint checking the database**, the simplest
  possible option -- rejected for the cascading-restart reason above; the
  moment this project reaches Kubernetes, this shape would need to be
  redone anyway, on a live cluster instead of on paper.
- **A single `/health` endpoint with no checks at all** (pure liveness,
  deferring readiness entirely to Phase 6) -- rejected; it would need
  revisiting the moment Phase 6 actually starts, for no savings now, and
  the two-endpoint shape is not meaningfully more code.
- **A third-party health check UI/dashboard package** (several exist for
  ASP.NET Core) -- rejected as unnecessary; two `curl`-able JSON
  endpoints are all this project or Kubernetes need, and adding a
  dashboard would be scope this phase doesn't call for.
