# 7. Dockerfile layout, multi-stage build, and base images

## Status

Accepted

## Context

Phase 3 needs to package `FinanceTracker.Api` as a container image before
`docker-compose` can wire it up with Postgres. Several decisions bundle
together here: which base images to build on, where the `Dockerfile`
lives relative to a multi-project solution where `Api` reaches into
`Application`, `Domain`, and `Infrastructure` via `ProjectReference`, and
how to structure the build so an ordinary code change doesn't re-restore
every NuGet package on the next `docker build`.

## Decision

1. **Multi-stage build**, three stages: `base` (runtime only), `build`
   (SDK), `final` (copies published output from `build` onto `base`).
   `base` uses `mcr.microsoft.com/dotnet/aspnet:10.0`; `build` uses
   `mcr.microsoft.com/dotnet/sdk:10.0`. The SDK image is never present in
   the final image -- only its build output is copied across with
   `COPY --from=build`.
2. Both are the standard Debian-based (`noble`) image variants, not the
   `chiseled` or Alpine variants Microsoft also publishes. Chiseled images
   ship no shell at all (`docker exec ... sh` simply fails), which makes
   debugging a container much harder while still learning Docker itself.
   Alpine's `musl` libc has a history of subtle globalization/ICU and
   Npgsql-adjacent issues versus `glibc`. Neither trade-off is worth
   making yet; revisit once comfortable with the basics, and especially
   before Phase 8's deploy to a resource-constrained free-tier VM, where
   image size starts to matter more than it does locally.
3. **`Dockerfile` lives at `src/FinanceTracker.Api/Dockerfile`** -- next
   to the project it builds -- but the **build context is the repository
   root**, not the project folder. `Api`'s `ProjectReference`s mean
   `Application`, `Domain`, and `Infrastructure` must all be reachable
   inside whatever context `COPY` draws from; scoping the context to
   `src/FinanceTracker.Api/` would make those `..\` references
   unreachable. This layout anticipates Phase 5 adding a second
   Dockerfile at `src/FinanceTracker.Worker/Dockerfile`, sharing the same
   repo-root context and the same solution layout, rather than needing to
   restructure anything then.
4. Only the four `.csproj` files are `COPY`'d and restored before the
   rest of `src/` is copied in, so Docker's layer cache keeps
   `dotnet restore`'s layer (a network-bound step) valid across
   ordinary `.cs`-only changes, and only the `dotnet publish` layer
   (compilation, disk-bound) re-runs on every code change.

## Consequences

**Positive:**

- Small final image -- no SDK, no compilers, no MSBuild -- and a fast
  inner loop, since restore is cached separately from compilation.
- One consistent pattern (`Dockerfile` beside its project, repo-root
  context) ready to extend to `Worker` in Phase 5 without rethinking the
  layout.
- Running as the base image's built-in non-root `$APP_UID` user is a
  small defense-in-depth measure at no extra cost.

**Negative:**

- Debian-based images are still meaningfully larger than chiseled or
  Alpine equivalents -- an accepted cost for now, in exchange for
  `docker exec`-ability while learning.
- Two Dockerfiles (`Api`, later `Worker`) will duplicate the same
  restore-layer boilerplate. A shared intermediate base stage could
  reduce this once `Worker` exists; not worth introducing speculatively
  for one Dockerfile.

## Alternatives Considered

- **Chiseled images** -- rejected for now; no shell makes early Docker
  debugging much harder, for a size saving that doesn't matter locally.
- **Alpine images** -- rejected; `musl`-related globalization/Npgsql
  quirks are a known source of hard-to-diagnose bugs, not worth trading
  for size at this stage.
- **A single-stage image built from the SDK base** -- rejected; ships
  compilers and build tooling in the image that actually runs, for no
  runtime benefit, and is meaningfully larger.
- **`Dockerfile` at the repository root** -- considered, since the build
  context has to be the root either way. Rejected in favor of keeping it
  beside the project it builds, which reads more clearly once a second
  Dockerfile (`Worker`, Phase 5) exists side by side with it.
