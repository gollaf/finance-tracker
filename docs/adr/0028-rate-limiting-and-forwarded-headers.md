# 28. Rate limiting and forwarded headers

## Status

Accepted

## Context

The server deployment (ADR 0025) is about to be made public behind a
reverse proxy, with registration open to anyone (ADR 0026). Two things
follow.

Anyone on the internet can call the API, including scripts that register
thousands of accounts or guess passwords at full speed. Nothing limits
that today.

Behind a reverse proxy, every request reaches the API from the proxy's
address. The visitor's own address arrives only in the `X-Forwarded-For`
header, which any client can also write itself. A limit per client address
needs the real address, and must not believe a forged one.

## Decision

1. **ASP.NET Core's built-in rate limiter, per client address.** A general
   limit applies to every endpoint (100 requests per minute), and the
   `/api/auth` endpoints carry a stricter policy on top of it (10 requests
   per 5 minutes). Both are fixed windows with no queue: a request over the
   limit is rejected at once.
2. **429 with `Retry-After` and a ProblemDetails body.** The middleware's
   default status, 503, would report a server failure.
3. **Health endpoints are never limited.** A rejected probe would look like
   a failing instance.
4. **The limits are configuration** (`RateLimiting:*`), read per request,
   so the tests can set tiny ones and the rest of the suite can lift them.
5. **Forwarded headers only from trusted networks.** The forwarded-headers
   middleware runs first in the pipeline and reads `X-Forwarded-For` and
   `X-Forwarded-Proto` only from the networks in
   `ReverseProxy:TrustedNetworks`. With none configured it reads no
   headers at all. Emptying ASP.NET Core's trust lists instead -- the
   common recipe, and what `ASPNETCORE_FORWARDEDHEADERS_ENABLED` does --
   makes the middleware trust every sender, which would let any visitor
   escape the limit by claiming a new address in each request.

## Consequences

**Positive:**

- Mass registration and password guessing are slowed to a crawl from any
  one address.
- Logs and limits see the visitor's real address once the proxy's network
  is configured, and a forged header from anyone else is ignored.
- The limits are tested end to end, including a forged header.

**Negative:**

- **Limits are per instance and in memory.** Two replicas allow twice the
  rate, and a restart resets the counters. A shared store (Redis) would
  fix both; one server doesn't need it.
- **Many visitors behind one address share a limit** (an office, a mobile
  carrier's NAT). The limits are generous enough for a demo.
- **An attacker with many addresses is not stopped**, only slowed per
  address. That needs a CAPTCHA or e-mail confirmation.
- **The proxy's network has to be known**, so the server deployment pins
  its Docker network's address range.

## Alternatives Considered

- **Rate limiting in the reverse proxy** -- Caddy has no rate limiter in
  its standard build; a plugin would mean building a custom image.
- **Partitioning by user** -- fairer for signed-in users, but the endpoints
  that most need a limit, register and login, have no user yet.
- **A sliding window or token bucket** -- smoother at window edges; a fixed
  window is the simplest to reason about and enough here.
- **`ASPNETCORE_FORWARDEDHEADERS_ENABLED=true`** -- no code at all, but it
  trusts every sender (see Decision 5).
