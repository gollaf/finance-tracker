# 26. Authentication with ASP.NET Core Identity

## Status

Accepted

## Context

Every endpoint of the API is open. Before the server deployment (ADR 0025)
can be made public, callers have to sign in, and later each user must see
only their own data. This record covers signing in; separating the data
per user is a decision of its own.

Constraints: free tooling only, no external service to depend on, and a
frontend (Angular) planned later. The API runs as one container on the
server and as two replicas in the local Kubernetes cluster (ADR 0020).

## Decision

1. **ASP.NET Core Identity with its built-in API endpoints.**
   `AddIdentityApiEndpoints` and `MapIdentityApi`, mounted under
   `/api/auth`: `register`, `login`, `refresh`, and the password, e-mail
   and two-factor endpoints that come with them. Users have `Guid` keys
   (`IdentityUser<Guid>`), so the rest of the system can refer to a user by
   a plain `Guid`.
2. **Bearer tokens.** `POST /api/auth/login` returns an access token (valid
   1 hour by default) and a refresh token (14 days), sent as
   `Authorization: Bearer <token>`. They are not JWTs: Identity encrypts
   them with ASP.NET Core Data Protection. Microsoft positions them for
   simple scenarios and clients that can't use cookies, and recommends
   cookies for browser apps; both are enabled (`/login?useCookies=true`),
   so the frontend can choose later.
3. **One DbContext, users only.** `FinanceTrackerDbContext` derives from
   `IdentityUserContext<IdentityUser<Guid>, Guid>`, so the Identity tables
   are created by the same migrations, the same `migrate` step and the same
   readiness check as everything else. `IdentityUserContext`, not
   `IdentityDbContext`: `AddIdentityApiEndpoints` sets Identity up without
   roles, so the three role tables `IdentityDbContext` adds would never be
   used. The tables are `AspNetUsers`, `AspNetUserClaims`,
   `AspNetUserLogins` and `AspNetUserTokens`.
4. **Data Protection keys are stored in the database**
   (`PersistKeysToDbContext`, application name `FinanceTracker`). By
   default the keys are kept in the container's file system: every
   redeploy would make all tokens unreadable, and each Kubernetes replica
   would have its own keys and reject tokens issued by the other.
5. **Sign-in is required on the controllers**
   (`MapControllers().RequireAuthorization()`), not through a global
   fallback policy. `MapIdentityApi`'s `/login` and `/register` carry no
   `AllowAnonymous`, so a fallback policy would also lock callers out of
   signing in. Health checks and the API docs stay open.
6. **The API docs can send the token.** An OpenAPI document transformer
   declares the bearer scheme and marks every operation except the sign-in
   endpoints as requiring it, so the token can be entered once in Scalar.

## Consequences

**Positive:**

- No external identity provider, account or secret to manage; users live
  in the existing PostgreSQL database.
- Tokens survive restarts and work across instances.
- Every new controller is protected without any extra attribute.
- The integration tests sign in through the real endpoints, so the whole
  path -- register, login, token, protected request -- is tested.

**Negative:**

- **Anyone can register.** That is the point of a public demo, but it
  invites automated sign-ups; rate limiting belongs to making the API
  public.
- **No e-mail is sent.** Without an e-mail sender, confirmation and
  password-reset e-mails go nowhere; confirmation is not required, and a
  forgotten password can't be recovered.
- **The keys are stored unencrypted.** Anyone who can read the database
  can forge tokens -- but they could read all the data anyway.
- **An access token can't be revoked** before it expires (up to one hour).
- **The Worker carries Identity and Data Protection assemblies** it never
  uses, because it shares the DbContext.
- **New endpoints outside the controllers are open by default**; they
  have to be protected explicitly.
- **Signed-in users still share all data** until ownership is added.

## Alternatives Considered

- **JWTs issued by the API itself** (`AddJwtBearer` plus a login endpoint)
  -- a standard, self-describing token, but the login, refresh and
  password handling would be hand-written, along with a signing key to
  manage.
- **An external identity provider** (Microsoft Entra ID, Auth0, a
  self-hosted Keycloak) -- the realistic production setup, but it adds an
  outside dependency, or roughly half a gigabyte of memory for Keycloak on
  a 6 GB server.
- **Cookies only** -- Microsoft's recommendation for browser apps, but
  awkward for API clients and the API docs. Both remain available.
- **`IdentityDbContext` with roles** -- the usual template choice; it adds
  role tables for role-based authorization ("Admin" and so on), which
  nothing here needs. Moving to it later is a migration away.
- **A separate DbContext for Identity** -- keeps the Identity tables out
  of the domain's DbContext, but needs a second migration history, and the
  `migrate` command and readiness check would have to handle both.
- **Data Protection keys in a file on a Docker volume** -- works on the
  single server, but every Kubernetes replica would need the same shared
  volume.
