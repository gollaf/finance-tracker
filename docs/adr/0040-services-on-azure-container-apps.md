# 40. Services on Azure Container Apps

## Status

Accepted. Builds on [ADR 0039](./0039-azure-foundation-in-bicep.md).

## Context

ADR 0039 created the network, the database, telemetry storage and the
apps identity. The services still need somewhere to run: RabbitMQ, the
Worker and the Api, plus the migration step before the code that needs it
(ADR 0017), as on the server, in Kubernetes and on ECS.

Azure Container Apps runs containers without servers or a cluster to
manage. Apps live in an *environment*, a shared network space where they
reach each other by name. An app runs as one or more *replicas*, copies of
its containers; each change to its containers or settings creates a new
*revision*. The Consumption profile bills per second of CPU and memory
used, with a monthly free grant, and can scale an app to zero replicas.

## Decision

1. **One environment**, `cae-finance-tracker`, of the workload-profiles
   kind with the Consumption profile, in `snet-apps`. It has a public entry
   point (`internal: false`) for apps that ask for external ingress. Azure
   creates a second resource group for the environment's own load balancer
   and address, named `<resource group>-infrastructure`.
2. **Console output to Log Analytics through Azure Monitor diagnostic
   settings** (`ContainerAppConsoleLogs`, `ContainerAppSystemLogs`) rather
   than the `log-analytics` destination, which needs the workspace's
   shared key. The .NET services log JSON, as on AWS.
3. **RabbitMQ as an app**, `finance-tracker-rabbitmq`: Docker's official
   image from Amazon ECR Public (no Docker Hub pull limits), 0.5 vCPU and
   1 GiB with the memory watermark at 600 MiB, exactly one replica, and
   internal TCP ingress on 5672: reachable as `finance-tracker-rabbitmq`
   from inside the environment, from nowhere else. Its storage is the
   replica's; messages not yet consumed are lost when the replica is
   replaced.
4. **The RabbitMQ password is derived, not stored**: a GUID computed from
   the subscription's and the resource group's IDs. It is the same on every
   deploy, so the broker and the Worker always agree; it is passed as a
   secure parameter and exists only in the two apps' secrets.
5. **The Worker**, exactly one replica, never scaled to zero: the outbox
   relay must run once, and polls the database without a request to wake
   it. No ingress, no probes.
6. **The Api** with external HTTPS ingress on an address and certificate
   Azure provides, HTTP redirected to HTTPS, liveness and readiness probes
   on the health endpoints, and zero to one replicas: it scales to zero
   when idle, and the first request after that waits for a replica to
   start. The environment's proxy, inside `snet-apps`, adds the visitor's
   address to `X-Forwarded-For`, so that subnet is the trusted network.
7. **Migrations as an init container** in both the Api and the Worker: the
   Api image's `migrate` command runs, and must succeed, before the app's
   container starts, like the container dependency on ECS. The identity
   is available to init containers (`lifecycle: All`), which the database
   sign-in needs.
8. **Passwordless settings for the code.** The apps get the connection
   string without a password, `Database__UseManagedIdentity=true` and
   `AZURE_CLIENT_ID` to sign in to PostgreSQL with a token, and
   `APPLICATIONINSIGHTS_CONNECTION_STRING` for telemetry. The code that
   reads the first two and sends to Application Insights is added before
   the first deploy.
9. **The build is a parameter**, `imageTag`, exactly 11 characters
   (`sha-` and 7), which also refuses `latest`. The images are the public
   ones on ghcr.io; no registry credentials.
10. **Sizes**: the Api and the Worker get 0.25 vCPU and 0.5 GiB, the
    migration step the same; together they make an allowed pair either
    way the platform counts init containers.

## Consequences

**Positive:**

- No servers, no cluster, no load balancer to define: HTTPS, DNS between
  apps and replica restarts come with the platform.
- The broker is unreachable from outside, and no password for the
  database or telemetry exists.
- The Api costs nothing while idle.

**Negative:**

- **Cold starts**: after a quiet spell the first request waits for the
  Api's replica, its migration step and .NET's startup.
- **The derived RabbitMQ password** is only as secret as the subscription
  and resource group IDs; anyone with access to the subscription could
  read it from the apps anyway.
- **The migration step runs on every replica start**, usually finding
  nothing to do.
- **A second resource group** appears, which the environment owns.
- **RabbitMQ's messages don't survive a replica replacement**; the outbox
  keeps what hasn't been published yet.

## Alternatives Considered

- **Azure Service Bus** -- a managed broker, but the code uses RabbitMQ's
  own client (ADR 0011), the Basic tier has no topics, and the tiers that
  do bill a base fee.
- **The RabbitMQ password in Key Vault** -- random rather than derived,
  but the template can't create a random value once and keep it; it would
  need a separate step to create the secret before the first deploy.
- **A Container Apps job for migrations** -- a one-off run per deploy, but
  one more step to start and wait for; the init container can't be
  forgotten.
- **Docker Hub images** -- the same image, but anonymous pulls are
  rate-limited per address, and the environment's address is shared.
- **The managed OpenTelemetry agent** -- sends telemetry from the
  environment, but not metrics to Application Insights, and it needs
  key-based ingestion switched on.
- **Azure App Service** -- built around web apps; a background Worker and
  a TCP broker fit it poorly.
- **AKS** -- Kubernetes is already covered locally (ADR 0019), and a
  cluster costs more than three small apps.
