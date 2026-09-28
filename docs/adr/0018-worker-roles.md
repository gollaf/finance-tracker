# 18. Worker roles: outbox relay and consumers as separately deployable parts

## Status

Accepted. Builds on ADR 0012 (topology) and ADR 0013 (transactional
outbox).

## Context

The Worker process does two different jobs:

- **The outbox relay** (ADR 0013) polls `OutboxMessages` and publishes
  each row to RabbitMQ. ADR 0013 records that it assumes a *single*
  running relay: two relays can read the same unpublished rows and publish
  them twice. Correct, because consumers are idempotent, but wasteful --
  and it gets worse with every extra copy.
- **The consumers** (ADR 0012) handle messages from their queues. They
  scale naturally: RabbitMQ hands each message to exactly one consumer on a
  queue, and prefetch 1 spreads the work evenly. Three consumer processes
  handle roughly three times the load.

Running several copies of the Worker, to handle a large CSV import faster
for example, means scaling the consumers. Today that also means scaling
the relay, because the two only exist together.

Separating them surfaces a second problem. Each consumer declared its own
queue when it started, and nothing else declared it. The publisher uses
`mandatory: true` (ADR 0012), so publishing a message whose routing key
matches no queue fails, and `OutboxRelay` counts that failure as an
attempt. After 10 attempts (about 20 seconds at a 2-second poll) the row is
no longer retried. While relay and consumers shared a process, the queues
existed within milliseconds of startup and this never mattered. With a
relay running on its own, it matters in two ordinary situations:

- The relay starts before any consumer process has, for example because a
  consumer pod is still pulling its image. A container orchestrator gives
  no guarantee about which starts first.
- Consumers are deliberately scaled to zero for a while. Every event
  published in that window would be given up on.

A durable queue is precisely the thing that keeps messages while nobody
is consuming. Its existence shouldn't depend on a consumer running.

## Decision

1. **Two switches, `Worker:RunOutboxRelay` and `Worker:RunConsumers`, both
   defaulting to `true`.** `AddWorker` registers the `OutboxRelay` only
   when the first is on, and the consumer hosted services only when the
   second is on. Everything else (the MediatR pipeline, EF Core, the
   RabbitMQ connection, the publisher) is registered either way. With both
   on, the Worker behaves exactly as before, so docker compose, `dotnet
   run` and the existing end-to-end tests are unchanged.
2. **Both off is a startup error.** The process would otherwise connect to
   everything and then do nothing, which is easy to miss in a deployment.
3. **The relay's registration moves out of `AddRabbitMqMessaging` into its
   own `AddOutboxRelay`**, so that talking to the broker and running the
   relay are separate decisions.
4. **Consumer queues are declared at startup by every Worker process,
   whatever its role.** Each consumer's queue is written down once as a
   `ConsumerQueue` (name, routing key, delivery limit), a public static
   field next to the consumer that uses it. `AddWorker` registers every
   one of them in DI, and `RabbitMqTopologyInitializer`, which already runs
   in every process that talks to the broker, declares them after the
   exchanges. Consumers still declare their own queue as well; declaring
   is idempotent.
5. **A scaled deployment runs one relay and N consumer processes**: in
   Kubernetes, an `outbox-relay` Deployment with `replicas: 1`,
   `strategy: Recreate` and `Worker__RunConsumers=false`, and a `worker`
   Deployment with `Worker__RunOutboxRelay=false` scaled as needed.
   `Recreate` stops the old relay before starting its replacement, so a
   rollout doesn't briefly run two.

## Consequences

**Positive:**

- Consumers can be scaled freely without duplicating the relay.
- A queue exists from the moment any Worker process has started. Events
  published while no consumer runs wait in their queue instead of being
  given up on in the outbox.
- The same image serves both roles; only configuration differs.

**Negative:**

- **One relay is still a design constraint, now enforced by deployment
  configuration rather than by code.** Setting `replicas: 2` on the relay
  Deployment, or running two compose Workers with the defaults, brings the
  duplicate publishes back. Row locking (`SELECT ... FOR UPDATE SKIP
  LOCKED`) remains the fix if several relays are ever needed.
- **The relay is a single point of delay, not of loss.** While it is down
  (a crash, a node failure, a `Recreate` rollout), events accumulate in
  `OutboxMessages` and are published once it is back.
- **A queue's settings now live in two places that must agree**: the
  initializer and the consumer declare it with the same `ConsumerQueue`,
  so they do by construction, but a queue declared with different
  arguments than an existing one is still rejected by the broker (ADR 0012).
- **A process can declare queues for consumers it doesn't run.** Harmless,
  and the point, but it means a queue for a consumer that has been removed
  from the code keeps existing on the broker until deleted by hand.

## Alternatives Considered

- **`SELECT ... FOR UPDATE SKIP LOCKED` in the relay**, so any number of
  relays share the work safely -- the most robust fix for duplicate
  publishes, but a real change to the relay's query and transaction
  handling for a load this project doesn't have. It also doesn't solve the
  missing-queue problem on its own.
- **Leader election** (only one of N Worker processes runs the relay at a
  time, for example through a Kubernetes Lease) -- keeps a single
  Deployment, but adds a coordination mechanism far heavier than a
  configuration switch.
- **Not counting an unroutable publish as a failed attempt**, so rows wait
  in the outbox until a queue appears -- the relay always takes the 50
  oldest rows, so stuck rows would block every newer one, and an event
  nobody subscribes to would be retried forever.
- **Documenting "start the consumers before the relay"** -- not something
  Kubernetes can guarantee, and it still loses events when consumers are
  scaled to zero.
