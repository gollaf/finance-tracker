# 33. Services on ECS: discovery, migrations and secrets

## Status

Accepted. Builds on [ADR 0032](./0032-aws-deployment-with-cdk-fargate-and-rds.md).

## Context

ADR 0032 put the system on ECS with Fargate, in a network without NAT
gateways, with RabbitMQ as a container. The `FinanceTracker-App` stack
now has to answer what compose and Kubernetes answer with their own
features: how the Worker finds RabbitMQ, how migrations run before the
code that needs them, how passwords reach the containers, and where logs
go.

Two facts shape the answers. Only the Worker talks to RabbitMQ; the Api
writes to the outbox and never connects to the broker (ADR 0018). And
the connection string the code reads, `ConnectionStrings:FinanceTracker`,
includes the password in compose -- but ECS can only inject a secret as
a whole environment variable, not splice it into a string.

## Decision

1. **One ECS cluster** on Fargate, tasks on **arm64** (Graviton). The
   images are already built for it (ADR 0024) and it costs less than
   x86 for the same size.
2. **RabbitMQ is its own service, found by DNS.** The cluster has a
   private DNS namespace, `finance-tracker.local` (AWS Cloud Map); the
   RabbitMQ service registers as `rabbitmq`, so the Worker connects to
   `rabbitmq.finance-tracker.local`, and the record follows the task to
   whatever address it gets. The image is Docker's official one from its
   mirror on Amazon ECR Public, which has no Docker Hub pull limits. The
   memory watermark is written into the broker's configuration as it
   starts, as in Kubernetes.
3. **One task per service, replaced rather than overlapped**
   (`minHealthyPercent 0`, `maxHealthyPercent 100`): one broker, and
   exactly one outbox relay, with both Worker roles in one process as on
   the server. A deploy whose tasks keep failing is rolled back by the
   deployment circuit breaker.
4. **Migrations run inside the task that needs them.** The Worker's task
   has a second, non-essential container: the Api image with its
   `migrate` command (ADR 0017). The Worker container starts only after
   it exited successfully -- ECS's version of compose's
   `service_completed_successfully`. The Api's task will do the same.
   EF Core takes a lock while migrating, so two tasks starting together
   wait for each other instead of migrating twice.
5. **Passwords come from Secrets Manager.** ECS reads them with the
   task's execution role and passes them in as environment variables.
   The connection string has no password: Npgsql reads `PGPASSWORD`
   when the string has none, and the database password is injected
   under that name. RabbitMQ's password is generated into a secret of
   its own. The task roles -- what the application code itself may do
   in AWS -- get no permissions; the code calls no AWS APIs. No Groq key
   is configured, so the AI features fall back as without one locally.
6. **Logs go to CloudWatch**, in one log group per service
   (`/finance-tracker/worker`, `/finance-tracker/rabbitmq`), kept for a
   week.
7. **The build is chosen on the command line**:
   `-c imageTag=sha-1234567`, one of the tags CI publishes. `latest` is
   refused: the task definition would not change when `latest` moves, so
   ECS would go on running whatever it pulled first. Without a tag, the
   application stack is left out of the app, so the data stack can be
   deployed on its own.
8. **Firewall rules for the database live in the application stack.**
   The rule letting the Worker reach PostgreSQL modifies the data
   stack's security group, but is created by the application stack. The
   data stack never depends on the application stack, so destroying the
   application also removes its access. References between the stacks
   are "weak" (CloudFormation's `Fn::GetStackOutput`, the CDK default
   for new projects): no exports lock the data stack in place.

## Consequences

**Positive:**

- No servers, and no extra moving parts: discovery is DNS, migrations
  are a container, secrets are injected by ECS.
- A Worker deploy leaves RabbitMQ and its queues alone.
- A failed deploy rolls back by itself.
- No password appears in a template, an environment variable definition
  or the repository.

**Negative:**

- **RabbitMQ's storage is temporary.** Fargate tasks have no lasting
  disk; if the broker's task is replaced, messages not yet consumed are
  lost. Acceptable for a copy for learning; the server keeps a volume.
- **A short gap on every Worker or broker deploy**: the old task stops
  before the new one starts.
- **Each task has a public IPv4 address**, billed by the hour, and the
  private DNS namespace is a Route 53 hosted zone, billed by the month.
- **Weak references don't stop deleting the data stack** while the
  application still runs against it; the application then fails until
  it is deployed again.
- **Destroying the application stack also needs `-c imageTag=...`**
  (any valid tag), because without one the stack isn't part of the app.

## Alternatives Considered

- **ECS Service Connect** -- discovery with retries and metrics, but it
  adds an Envoy proxy container to every task, using memory the small
  tasks don't have.
- **RabbitMQ as a second container in the Worker's task**, reached on
  `localhost` -- no discovery needed, but every Worker deploy would
  restart the broker and drop its queues.
- **A load balancer in front of RabbitMQ** -- a stable address, billed
  by the hour for one client.
- **Migrations as a separate one-off task** started by hand or by CI --
  one more step to remember; the container dependency makes it
  impossible to forget.
- **A second secret with the whole connection string** -- would copy the
  database password into another place to rotate; `PGPASSWORD` needs no
  copy and no code change.
- **Pulling RabbitMQ from Docker Hub** -- the same image, but anonymous
  pulls are rate-limited per address.
