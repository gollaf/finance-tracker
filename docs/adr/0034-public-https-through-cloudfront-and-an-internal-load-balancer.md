# 34. Public HTTPS through CloudFront and an internal load balancer

## Status

Accepted. Builds on [ADR 0033](./0033-services-on-ecs-discovery-migrations-and-secrets.md);
amends [ADR 0028](./0028-rate-limiting-and-forwarded-headers.md).

## Context

The Api on AWS needs a public HTTPS address. On the server, Caddy gets a
certificate for a DuckDNS name (ADR 0029). On AWS a certificate from the
certificate service needs a domain whose DNS records we control, and
DuckDNS offers none of the record types it validates with; buying a
domain would break the free-only rule.

The rate limiter (ADR 0028) needs each visitor's own address. Behind
CloudFront and a load balancer, X-Forwarded-For holds the visitor's
address followed by CloudFront's; the Api reads one hop back, so it would
see CloudFront's edge servers and limit thousands of visitors as one.

## Decision

1. **CloudFront in front.** A distribution serves HTTPS on its own
   `*.cloudfront.net` name with CloudFront's certificate, and redirects
   HTTP to HTTPS. It caches nothing (the managed CachingDisabled policy)
   and passes on every header the visitor sent, Authorization and Host
   included, plus CloudFront's own headers (the managed
   AllViewerAndCloudFrontHeaders-2022-06 policy). The cheapest price
   class (edge locations in Europe and North America) is enough.
2. **An internal load balancer as a VPC origin.** The Application Load
   Balancer has no public address and sits in the isolated subnets.
   CloudFront reaches it through a VPC origin, over AWS's network, in
   plain HTTP: the load balancer can't have a certificate without a
   domain either. Its security group admits only the security group
   CloudFront creates for VPC origins; CloudFormation doesn't return that
   group's ID, so a custom resource looks it up once after the
   distribution exists.
3. **The Api behind it**, as an ECS service like the Worker: a migrate
   container first, then the Api. Unlike the Worker, a new task must pass
   the load balancer's health check (`/health/ready`) before the old one
   stops, so a deploy has no gap. Only the load balancer may connect to
   it.
4. **The visitor's address from CloudFront's own header.** CloudFront
   sets `CloudFront-Viewer-Address` (`address:port`) and
   `CloudFront-Forwarded-Proto` itself. The Api's forwarded-headers setup
   gets two settings, `ReverseProxy:ForwardedForHeader` and
   `ReverseProxy:ForwardedProtoHeader`, that name the headers to read
   instead of `X-Forwarded-For` and `X-Forwarded-Proto`; unset, nothing
   changes. The load balancer, inside the VPC, is the trusted network.
   The proto header matters too: the load balancer replaces
   `X-Forwarded-Proto` with its own scheme, `http`.
5. **IPv4 only.** CloudFront documents the format of the viewer-address
   header for IPv4; an IPv6 address followed by a port can't be split
   reliably, so the distribution doesn't accept IPv6.

## Consequences

**Positive:**

- HTTPS at no cost and without a domain.
- No public load balancer: nothing in the VPC is reachable from the
  internet except through CloudFront, and no public IPv4 address is
  billed for the load balancer.
- Rate limiting sees each visitor, from a header no visitor can set
  through CloudFront.
- Deploys of the Api cause no interruption.

**Negative:**

- **A `cloudfront.net` name**, not a memorable one.
- **CloudFront to the load balancer is plain HTTP.** It stays on AWS's
  network inside the VPC origin, but it is not encrypted end to end.
- **The viewer-address header is only as trustworthy as the path.**
  Anything able to reach the load balancer directly could set it; only
  CloudFront can, which the security group and the internal scheme
  ensure.
- **A custom resource** (a small Lambda function) runs once, when the
  stack is created, to find CloudFront's security group.
- **No IPv6** for visitors.
- **A load balancer bills by the hour**, which is why the application
  stack is destroyed when not in use.

## Alternatives Considered

- **HTTP only, on a public load balancer** -- simplest, but login tokens
  would travel unencrypted.
- **A domain of our own with an ACM certificate** -- a real name and
  HTTPS to the load balancer, but a domain costs money every year.
- **A public load balancer restricted to CloudFront's prefix list** --
  the older pattern; the load balancer keeps a public address, and the
  prefix list admits every CloudFront distribution, not only ours.
- **Walking X-Forwarded-For** -- would mean trusting CloudFront's edge
  addresses, a long list that changes; the dedicated header avoids it.
- **API Gateway in front of the Api** -- HTTPS on an AWS name too, but
  another service to learn and configure, with a VPC link of its own to
  reach the private load balancer.
