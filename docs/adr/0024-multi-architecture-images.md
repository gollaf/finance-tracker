# 24. Multi-architecture images by cross-compiling

## Status

Accepted. Amends the build stage of ADR 0007.

## Context

The always-on demo will run on an Oracle Cloud Always Free virtual
machine. Of the two Always Free compute shapes, the AMD one has 1 GB of
memory and 1/8 of a CPU core -- not enough for PostgreSQL, RabbitMQ, the
Api and the Worker together. The other, Ampere A1, offers up to 2 cores and
12 GB, but its processors are Arm (`arm64`), not x86 (`amd64`).

A container image is built for one processor architecture: a program
compiled for x86 can't run on Arm. Today CI builds and publishes `amd64`
images only (ADR 0022), so an Arm server can't run them.

A registry can store several builds of one image under a single tag,
listed in an *image index* (also called a manifest list). `docker pull`
reads the index and downloads the build that matches the machine it runs
on, so the same `ghcr.io/...:sha-<commit>` works on a laptop and on the
Arm server.

There are three ways to produce an `arm64` build on an `amd64` CI runner:

- **Emulation with QEMU.** Docker runs the whole Dockerfile as if on an
  Arm machine, translating every instruction. No Dockerfile change, but
  much slower, and Microsoft states that .NET is not supported under QEMU:
  `dotnet restore` and `dotnet publish` would run under exactly that.
- **Native Arm runners.** GitHub offers free `ubuntu-24.04-arm` runners
  for public repositories. Each architecture builds natively, but every
  image then needs one job per architecture plus a job that merges the
  results into one index -- a much larger workflow.
- **Cross-compiling.** The SDK runs on the build machine's own
  architecture and is told to produce output for the target one. This is
  the pattern Microsoft's own .NET sample Dockerfiles use. It works
  because the compiled application is mostly platform-neutral IL; only the
  runtime that executes it is architecture-specific, and that comes from
  the base image.

## Decision

1. **Both Dockerfiles cross-compile.** The build stage is pinned to the
   build machine with `FROM --platform=$BUILDPLATFORM`, and `dotnet
   restore` and `dotnet publish` get `-a $TARGETARCH`. BuildKit sets
   `BUILDPLATFORM` and `TARGETARCH` automatically from `--platform`.
   `publish` now uses `--no-restore`, because the restore layer has
   already restored for the same architecture.
2. **The runtime stages run nothing.** `base` and `final` only set
   metadata and copy files, so building them for a foreign architecture
   needs no emulation. This is a rule to keep: a `RUN` added there would
   execute under emulation.
3. **CI builds `linux/amd64` and `linux/arm64`** for both images, in the
   existing `docker-image` jobs, with no QEMU setup step. Job names, tags
   and the master-only push are unchanged.
4. **Local builds are unchanged.** `docker compose up` and `docker build`
   build for the machine they run on, as before.

## Consequences

**Positive:**

- One tag, two architectures: the Oracle Arm server, an x86 laptop and
  the cloud sprints can all pull the same `sha-<commit>`.
- No emulation anywhere in the build, and no change to the workflow's
  shape or to the required check names.

**Negative:**

- **CI image builds take longer**: the build stage runs once per
  architecture. Both run natively on the runner, and the GitHub Actions
  layer cache covers each.
- **The `arm64` images are never run in CI.** They are built, not
  started, so a problem that only appears on Arm at runtime shows up on
  the server first. .NET's cross-compilation is mature and nothing in this
  code is architecture-specific, so the risk is low.
- **The package page on GitHub may lose its description**: for a
  multi-architecture image GitHub reads it from annotations on the image
  index, not from the image labels this workflow sets. Both packages are
  already connected to this repository, and that connection stays.
- **Builds need BuildKit**, which supplies `BUILDPLATFORM` and
  `TARGETARCH`. It is the default builder in current Docker; the legacy
  builder doesn't set them.

## Alternatives Considered

- **QEMU emulation** -- the smallest change, but slow, and .NET is not
  supported under it.
- **Native Arm runners with a merge job** -- no Dockerfile change and
  every build native, at the cost of twice the jobs and a merge step;
  worth it only if a build ever has to *run* something on the target
  architecture.
- **Only `arm64`** -- the server would work, but a laptop or an x86 cloud
  service could no longer run the published images.
- **The AMD Always Free shape, keeping `amd64` only** -- no image change,
  but 1 GB of memory can't hold the whole system.
