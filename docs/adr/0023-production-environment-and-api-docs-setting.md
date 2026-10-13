# 23. Production environment and the API docs setting

## Status

Accepted

## Context

docker compose and the local Kubernetes manifests run the Api in the
`Development` environment (ADRs 0008 and 0020). A server reachable from
the internet should run in `Production`. Checked against `Program.cs`,
switching the environment changes exactly two things in this application:

- **Exception details.** In Development an unhandled exception's full
  text, stack trace included, is added to the 500 ProblemDetails
  response. A database exception's text can contain host names and SQL
  statements. In Production the response is a plain 500.
- **API docs.** `/openapi/v1.json` and the Scalar UI at `/scalar/v1` are
  only mapped in Development.

It does *not* change startup migrations: those follow
`Database:ApplyMigrationsOnStartup` (ADR 0017), whatever the environment.
Outside the application code, ASP.NET Core also stops loading
`appsettings.Development.json` and User Secrets in Production.

The two changes pull in different directions for a public demo. Exception
details should never reach a stranger. The API docs are what lets a
reviewer try the API without reading the source, and they describe only
the endpoints, which the README already lists.

## Decision

1. **Servers run in `Production`.** Local compose and Minikube stay in
   `Development`.
2. **The API docs are controlled by a setting, `ApiDocs:Enabled`, that
   defaults to "is this Development".** Nothing changes locally. A
   deployment that wants the docs sets `ApiDocs__Enabled=true` and keeps
   every other Production behavior, exception details included.
3. The setting is read after `Build()`, like
   `Database:ApplyMigrationsOnStartup`, so test configuration applies to
   it.

## Consequences

**Positive:**

- A public deployment can show interactive docs without also exposing
  exception details.
- The default is safe: forgetting the setting on a server means no docs,
  not too much information.

**Negative:**

- One more setting to know about when deploying.
- Public docs make the API easier to explore for everyone, not only
  reviewers. They add nothing an attacker couldn't learn from this public
  repository, and authentication, not obscurity, is what protects the data.

## Alternatives Considered

- **Run the server in `Development`** -- the docs come for free, but so do
  exception details in every 500 response.
- **Docs always on, in every environment** -- removes the setting, but a
  deployment that doesn't want them would then need a code change.
- **A custom `Demo` environment** -- `IsDevelopment()` and `IsProduction()`
  both return false for it, so every environment check in the framework
  and in libraries would have to be reasoned about again. A single named
  setting is easier to see and to test.
