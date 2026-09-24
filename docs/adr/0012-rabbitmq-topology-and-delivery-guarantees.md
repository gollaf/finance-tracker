# 12. RabbitMQ topology and delivery guarantees

## Status

Accepted

## Context

ADR 0011 chose RabbitMQ with the raw `RabbitMQ.Client` library, which
means this project -- not a framework -- decides how exchanges and queues
are laid out, when a message counts as "done", what happens when handling
it fails, and how a publisher knows the broker actually has it. Every
consumer built later inherits these answers through one shared base class
(`RabbitMqConsumer<TMessage>`), so they are decided once, here.

The underlying constraint is that a message broker cannot give
*exactly-once* delivery across crashes. A consumer can finish its work and
then crash before its acknowledgement reaches the broker; the broker then
has no way to know the work was done, and redelivers. The choice is only
between *at-most-once* (acknowledge first, and lose the message if the
work then fails) and *at-least-once* (acknowledge after, and sometimes
process a message twice).

## Decision

### 1. Topology, declared from code

- One durable **topic** exchange, `finance-tracker.events`, that every
  event is published to. A topic exchange routes by routing-key pattern,
  so a new consumer subscribes by binding its own queue -- the publisher
  never changes.
- One durable **direct** exchange, `finance-tracker.dead-letter`.
- **One queue per consumer**, each with its **own dead-letter queue**
  (`<queue>.dead-letter`), bound to the dead-letter exchange with the
  consumer queue's name as the routing key. A message that failed in one
  consumer never ends up mixed in with another consumer's failures.
- Everything is declared by `RabbitMqTopology` from code, idempotently, by
  whichever process needs it, on every startup -- never configured by hand
  in the management UI. A brand-new broker (a new machine, a
  Testcontainers instance, a Kubernetes pod) needs no manual setup.

### 2. Quorum queues with a delivery limit

Every consumer queue is a **quorum queue** (`x-queue-type: quorum`),
RabbitMQ's recommended durable queue type since 4.0, and the only one that
counts failed deliveries per message and enforces a limit on them
(`x-delivery-limit`, set to 5 by default). Past the limit, the broker
itself dead-letters the message (`x-dead-letter-exchange`). The
poison-message protection therefore lives in the broker, not in retry
bookkeeping this project would otherwise write and get wrong.

### 3. At-least-once consumption

- `autoAck: false`, and a message is acknowledged only **after**
  `HandleAsync` completes.
- Prefetch of 1: the broker sends a consumer its next message only once
  the previous one is settled. This bounds memory, bounds how much gets
  redelivered after a crash, and matches the external rate limits these
  consumers will call (Groq's free tier).
- Every `HandleAsync` must be **idempotent**. Each message carries a
  `MessageId` that stays the same across redeliveries, for handlers that
  need it, though checking current state ("is this already done?") is
  usually simpler.
- One DI scope per message, as ASP.NET Core does per request, so each
  message gets its own `DbContext`.

### 4. How each outcome is settled

| Outcome | Settled with | Effect |
|---|---|---|
| Handled successfully | `basic.ack` | Removed from the queue. |
| `HandleAsync` threw | `basic.reject`, requeue | Retried; counts towards the delivery limit, then dead-lettered. |
| Body isn't valid JSON | `basic.reject`, no requeue | Dead-lettered immediately -- retrying can't fix it. |
| Host shutting down mid-handle | `basic.nack`, requeue | Returned for later; **not** counted as a failure. |

The reject/nack split is deliberate. Since RabbitMQ 4.3, quorum queues
count only genuine failures -- `basic.reject`, or a crashed channel --
towards `x-delivery-limit`; `basic.nack` returns a message without
counting it. Using `nack` for failures would retry a poison message
forever. Using `reject` for a clean shutdown would spend one of a
message's attempts on something that wasn't its fault.

### 5. Reliable publishing

- **Publisher confirmations**, with tracking: `BasicPublishAsync`
  completes only once the broker confirms it has stored the message, and
  throws a `PublishException` if the broker refuses it. Without them, a
  publish "succeeds" as soon as the bytes reach the socket.
- **`mandatory: true`**: a message whose routing key matches no bound
  queue is returned to the publisher (a `PublishException` with
  `IsReturn`) instead of being silently discarded by the exchange.
- **Persistent** messages (`delivery_mode = 2`), so a message waiting in a
  durable queue survives a broker restart.

### 6. Connections

- One connection per process (`RabbitMqConnectionProvider`, a Singleton);
  one channel per consumer, plus one for the publisher.
- The client library does not retry an *initial* connection, so the
  provider retries it every 5 seconds until the broker is reachable.
  After that, the library's automatic recovery reconnects and restores
  channels, consumers, and topology by itself.
- Only processes that need the broker call `AddRabbitMqMessaging` -- the
  Worker. The Api has no RabbitMQ configuration or connection.

### 7. Message format

The body is JSON (`JsonSerializerDefaults.Web`, the same conventions as
the HTTP API), shared by both sides through `MessageSerialization`. The
message id and type travel as AMQP properties (`message_id`, `type`),
not inside the body.

## Consequences

**Positive:**

- No message is silently lost on either side: the broker confirms every
  publish, refuses unroutable ones, and never deletes a message before
  its handler has succeeded.
- A message that can never succeed stops being retried after a bounded
  number of attempts and waits, intact and inspectable, in its own
  dead-letter queue.
- Consumers only implement `QueueName`, `RoutingKey`, and `HandleAsync`;
  every reliability rule above is inherited, not reimplemented.

**Negative:**

- Retries are **immediate**, with no backoff between attempts. A short
  outage of something a handler depends on (Groq rate-limiting, for
  instance) can use up all of a message's attempts within seconds and
  dead-letter it. Acceptable while volume is tiny; the fix, if needed, is
  a delayed-retry queue (a TTL queue that dead-letters back into the
  main one).
- Dead-lettered messages are never replayed automatically -- someone has
  to inspect them in the management UI and move or delete them by hand.
- A queue's arguments are fixed once it exists. Changing, say, the
  delivery limit of an existing queue makes the broker reject the
  declare (`PRECONDITION_FAILED`) until that queue is deleted.
- A consumer channel closed by a *broker-side error* (as opposed to a
  lost connection, which recovers automatically) is not re-created;
  that consumer stops until the Worker restarts. Errors like that point
  to a code or configuration bug, and should be loud rather than papered
  over.
- Idempotency is a rule every handler author has to follow; the base
  class cannot enforce it.

## Alternatives Considered

- **`autoAck: true`** -- rejected; that is at-most-once. A crash or an
  exception in a handler would lose the message.
- **Classic queues plus a retry-count header** (republish a copy with an
  incremented header, ack the original, give up after N) -- rejected. It
  is more code, and republishing plus acking is two operations with the
  same crash-between-them problem the outbox exists to solve. Quorum
  queues' built-in delivery limit does the same thing inside the broker.
- **`basic.nack` for failures** -- rejected; it isn't counted towards
  the delivery limit on RabbitMQ 4.3+, so a poison message would loop
  forever (see decision 4).
- **Delayed retries via TTL queues now** -- deferred, not rejected; see
  Consequences.
- **A connection per consumer** -- rejected; connections are the
  expensive resource, channels exist to share one.
