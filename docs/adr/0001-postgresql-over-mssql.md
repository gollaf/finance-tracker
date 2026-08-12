# 1. Use PostgreSQL instead of MSSQL

## Status

Accepted

## Context

The application needs a relational database for accounts, transactions,
categories, and budgets. As a .NET developer, MSSQL is the default,
familiar choice, but this project is also meant to build skills that
transfer well to a wider range of job listings and to run cheaply in
Docker/Kubernetes without licensing friction.

Two options were considered:

- **MSSQL (Express edition)** — free to use, but heavier as a container
  image, Windows-oriented licensing model that gets complicated once you
  move past Express in a cloud/production context, and less common outside
  Windows-centric shops.
- **PostgreSQL** — fully open-source, lightweight official Docker image,
  no licensing tier to worry about, and increasingly the default pairing
  with .NET in modern job postings.

## Decision

Use **PostgreSQL**, accessed via EF Core with the Npgsql provider.

## Consequences

**Positive:**

- No licensing considerations at any scale — same behavior locally, in
  Docker, and on any cloud provider.
- Official Postgres Docker image is small and starts fast, which matters
  for local development and Testcontainers-based integration tests.
- Broader relevance: PostgreSQL is common not just in .NET job postings
  but across the wider backend job market, so the experience transfers.

**Negative:**

- Slightly less familiar than MSSQL coming from a typical .NET background;
  a few EF Core provider quirks (e.g. case-sensitive identifiers, some
  Npgsql-specific type mappings) require a small amount of extra learning.
- No built-in tooling equivalent to SSMS — relying on pgAdmin, DBeaver, or
  CLI (`psql`) instead.

## Alternatives Considered

- **MSSQL Express** — rejected due to container weight and cloud licensing
  complexity for a project explicitly built around free, portable
  deployment (Docker, Kubernetes, multiple cloud free tiers).
- **SQLite** — considered for pure simplicity, but rejected because it
  doesn't reflect a realistic production setup and would undercut the
  learning goals around EF Core migrations against a real server-based DB.
