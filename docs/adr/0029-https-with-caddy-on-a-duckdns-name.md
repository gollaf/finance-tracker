# 29. HTTPS with Caddy on a DuckDNS name

## Status

Accepted. Amends [ADR 0025](./0025-single-server-deployment-with-docker-compose.md):
the Api is now reachable from the internet, through Caddy.

## Context

Signing in (ADR 0026), per-user data (ADR 0027) and rate limiting
(ADR 0028) are in place, so the server deployment can be made public.
Until now it has been reachable only through an SSH tunnel.

A public API needs HTTPS: passwords and tokens must not cross the internet
in plain text, and browsers distrust plain HTTP. A trusted certificate
needs a name that points to the server. The constraint, as everywhere in
this project, is free tooling only, so a bought domain is out.

The server sits behind two firewalls that are closed by default: Oracle's
security list in front of the VM, and iptables inside Ubuntu.

## Decision

1. **A free DuckDNS subdomain** (`<name>.duckdns.org`) points to the VM's
   public IP. `duckdns.org` is on the Public Suffix List, so Let's Encrypt
   counts each subdomain as a domain of its own for its rate limits.
2. **Caddy as the reverse proxy**, in its own container from the official
   image, pinned to a version (`caddy:2.11.7`). It is the only container
   with public ports: 443 for HTTPS, and 80 for Let's Encrypt's HTTP
   challenge and the redirect to HTTPS. It obtains and renews the
   certificate by itself and passes requests to `api:8080` over the compose
   network. Its configuration is a single site block in
   `deploy/server/Caddyfile`, with the host name taken from `PUBLIC_HOST`.
3. **The certificate lives in a named volume** (`caddy-data`). Without it,
   every recreated container would request a new certificate and soon hit
   Let's Encrypt's limit of 5 identical certificates a week.
4. **HTTP/1.1 and HTTP/2 only.** HTTP/3 runs over UDP 443, which would need
   one more port in both firewalls for little gain.
5. **Caddy has a fixed address, and only that address is trusted.** The
   compose network's range is pinned (`172.30.0.0/24`, with Docker's
   automatic addresses limited to the upper half) and Caddy gets
   `172.30.0.10`. The Api trusts forwarded headers from `172.30.0.10/32`
   only (ADR 0028), not from the whole network: requests through the SSH
   tunnel arrive from the network's gateway and can't claim another address.
6. **The Api keeps its `127.0.0.1:5000` port** for the SSH tunnel; the
   RabbitMQ management UI stays tunnel-only.
7. **`AllowedHosts` stays `*`.** Caddy forwards only requests for
   `PUBLIC_HOST`, so the Api never sees another public host name, and a
   stricter list would break the tunnel, which sends `Host: localhost`.

## Consequences

**Positive:**

- The demo is reachable at a stable HTTPS address with a trusted
  certificate, at no cost and with no certificate to renew by hand.
- Only ports 80 and 443 are open to the internet, and only Caddy listens
  on them.
- The Api, the database and the broker keep no public ports.

**Negative:**

- **DuckDNS is a free service run by volunteers.** If it is down or goes
  away, the name stops resolving. Moving to another name means changing
  `PUBLIC_HOST` and the DNS record; Caddy fetches a new certificate itself.
- **A subdomain of duckdns.org** looks less polished than an own domain.
- **The VM's public IP must stay the same**, or the DNS record must be
  updated.
- **The pinned network range** must not collide with another network on
  the server.
- **One more container** to keep updated: the Caddy version is pinned and
  has to be raised deliberately.

## Alternatives Considered

- **nginx with certbot** -- the classic pair, but two pieces to configure
  and a renewal job to schedule; Caddy does both by default.
- **Traefik** -- automatic HTTPS too, but configured through container
  labels and a larger configuration surface than this single site needs.
- **sslip.io or nip.io names** (an IP address inside the name) -- no
  sign-up, but they are not on the Public Suffix List, so every user of
  the service shares one Let's Encrypt limit.
- **A Let's Encrypt certificate for the bare IP address** -- available
  since 2026, but valid for 6 days only, and Caddy's support still has
  open issues.
- **A bought domain** -- the most professional look, but a yearly cost.
- **Opening the Api port directly with Kestrel's HTTPS** -- no proxy, but
  the Api would have to obtain and renew certificates itself.
