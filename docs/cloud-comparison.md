# Oracle, AWS and Azure compared

The same system -- the Api, the Worker, RabbitMQ and PostgreSQL -- prepared
for three platforms in Phase 8. Oracle runs the permanent demo, AWS was
deployed during a timeboxed sprint, and Azure was prepared but never deployed
([ADR 0042](./adr/0042-azure-sprint-closed-without-a-deploy.md)). Each row
links to the decision behind it.

## At a glance

| | Oracle Cloud | AWS | Azure |
|---|---|---|---|
| State | Running, permanent demo | Deployed during the sprint | Templates only, not deployed |
| Account | Always Free, no upgrade | Free plan, $100 of credit | Free account refused |
| Compute | One Arm VM, docker compose ([0025](./adr/0025-single-server-deployment-with-docker-compose.md)) | ECS on Fargate, arm64 ([0032](./adr/0032-aws-deployment-with-cdk-fargate-and-rds.md), [0033](./adr/0033-services-on-ecs-discovery-migrations-and-secrets.md)) | Container Apps, Consumption profile ([0040](./adr/0040-services-on-azure-container-apps.md)) |
| Database | PostgreSQL container with a volume | RDS, `db.t4g.micro` | Flexible Server, Burstable B1ms ([0039](./adr/0039-azure-foundation-in-bicep.md)) |
| Broker | RabbitMQ container | RabbitMQ task, found through Cloud Map | RabbitMQ app, internal TCP ingress |
| Public HTTPS | Caddy, Let's Encrypt, DuckDNS name ([0029](./adr/0029-https-with-caddy-on-a-duckdns-name.md)) | CloudFront to an internal load balancer ([0034](./adr/0034-public-https-through-cloudfront-and-an-internal-load-balancer.md)) | External ingress with Azure's certificate |
| Database credentials | Password in the server's `.env` | Password in Secrets Manager, injected | None: Entra tokens for a managed identity |
| Infrastructure as code | Compose file and scripts | CDK in C#, two CloudFormation stacks | Bicep, two deployments |
| Deploys | GitHub Actions over SSH, after approval ([0031](./adr/0031-deploying-from-github-actions-over-ssh.md)) | From the laptop ([0036](./adr/0036-aws-deploys-from-the-laptop.md)) | Keyless from GitHub, designed ([0041](./adr/0041-azure-bootstrap-and-keyless-github-sign-in.md)) |
| Monitoring | Readiness check after each deploy | CloudWatch: JSON logs, 5 alarms, dashboard ([0035](./adr/0035-monitoring-on-aws-with-cloudwatch.md)) | Application Insights and Log Analytics, designed |
| Teardown | Not needed | `cdk destroy`, then the bootstrap | Delete the resource group |

## Compute

- **Oracle**: one virtual machine runs everything with docker compose.
  Nothing scales and nothing is managed for us, but it costs nothing and
  is simple to reason about: one file says what runs.
- **AWS**: each service is an ECS task on Fargate, with no servers to
  manage. Migrations run as a container the service's main container
  waits for; RabbitMQ and the Worker are replaced rather than overlapped
  so only one broker and one outbox relay ever run.
- **Azure**: each service is a container app. Migrations run as an init
  container; the Api scales to zero when idle. Container Apps hides more
  than ECS does: no task definitions, load balancer or service discovery to
  declare.

## Network and exposure

- **Oracle**: only Caddy publishes ports (80 and 443). The Api, database
  and broker are bound to the loopback address or the compose network.
- **AWS**: a VPC without NAT gateways. The containers get public
  addresses only to pull images, with security groups that admit nothing
  from the internet; the database and the load balancer live in isolated
  subnets.
- **Azure**: a VNet with two delegated subnets. The database has private
  access only, its name resolved by a private DNS zone; the broker is
  reachable only inside the Container Apps environment.

## Secrets and identity

- **Oracle**: passwords in a `.env` file readable only by its owner.
  Backups go to Object Storage through a write-only pre-authenticated URL.
- **AWS**: passwords generated into Secrets Manager and injected by ECS;
  the containers' task roles have no permissions at all.
- **Azure**: no password for the database or for telemetry. The containers
  run as a user-assigned managed identity that PostgreSQL and Application
  Insights trust; only RabbitMQ keeps a password, derived in the template.

## Deploying from CI

- **Oracle**: GitHub Actions connects over SSH with a key that can only run
  `deploy.sh`, after an approval. The script backs up, deploys, waits for
  readiness and rolls back on failure.
- **AWS**: keyless sign-in with OIDC was designed and refused by a service
  control policy of the account's AWS-managed organization; deploys run
  from the laptop with short-lived credentials.
- **Azure**: keyless sign-in through a federated credential, limited to
  one resource group and one assignable role. GitHub's token subject was
  verified; no subscription existed to sign in to.

## Observability

All three run the same OpenTelemetry instrumentation
([ADR 0037](./adr/0037-telemetry-with-opentelemetry.md)), exported only
where an OTLP endpoint is configured; locally it goes to the Aspire
Dashboard, with traces that continue through the outbox
([ADR 0038](./adr/0038-trace-context-through-the-outbox.md)).

- **Oracle**: `docker compose logs`, plus the deploy's readiness check.
- **AWS**: JSON logs in CloudWatch, metric filters counting errors, five
  alarms e-mailed through SNS, and a dashboard.
- **Azure** (designed): Application Insights accepting only Entra-signed
  telemetry, console logs into Log Analytics through a diagnostic setting.

## Cost

- **Oracle**: free for as long as the account exists, within the Always
  Free shapes.
- **AWS**: everything billed by the hour -- the database, the load
  balancer, public IPv4 addresses -- came out of the credit while the
  application stack existed.
- **Azure**: sized to the free account's amounts. Without the free
  account, pay-as-you-go has no cap; the Worker and RabbitMQ running all
  the time would go beyond the free grant.

## What each one taught

- **Oracle**: running a server yourself -- firewalls in two places, HTTPS
  with a free certificate, backups that are restored to count, deploys
  that roll back.
- **AWS**: networking from the route table up, the difference between the
  roles a container runs as, and how an organization's policy outranks an
  account's own administrator.
- **Azure**: identities in place of secrets, private networking for a
  managed database, and an eligibility check that can stop a plan before
  any of it runs.
