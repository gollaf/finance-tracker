# 13. Transactional outbox

## Status

Accepted

## Context

ADR 0011 moves work such as AI categorization into the Worker, triggered
by integration events published to RabbitMQ. The event has to be
published as a result of a database change -- "a Transaction was
added" -- and that is two writes to two different systems:

```csharp
await _transactionRepository.AddAsync(transaction); // 1. Postgres
await _publisher.PublishAsync(transactionAdded);    // 2. RabbitMQ
```

No transaction spans both, so the obvious code has a gap: if the process
crashes, or RabbitMQ is unreachable, between the two lines, the
Transaction is saved and the event is lost -- silently, with nothing left
to retry it. Swapping the order is worse: a consumer can receive an event
for a change that then fails to save. This is the *dual-write problem*.

Two facts about this codebase shape the solution:

- Every repository calls `SaveChangesAsync` itself (`AddAsync` saves
  immediately); there is no separate unit of work.
- The DbContext is Scoped, so every repository in one HTTP request (or,
  in the Worker, one message) already shares a single
  `FinanceTrackerDbContext` instance.

## Decision

### 1. Store the event in the database, in the same transaction

Integration events are written to an `OutboxMessages` table in the same
database, by the same `SaveChangesAsync` call that writes the change the
event describes. EF Core runs all of one `SaveChangesAsync`'s inserts and
updates in a single database transaction, so the event and the change are
committed together or not at all. Publishing to RabbitMQ becomes a
separate, retryable step that reads from that table.

### 2. Application sees only `IOutbox`

- `IIntegrationEvent` (Application): a past-tense record with a
  `static abstract string EventName`, e.g. `"transaction.added"`, used as
  the routing key.
- `IOutbox.Enqueue(event)` (Application port), implemented by
  `EfCoreOutbox` (Infrastructure), which adds an `OutboxMessage` to the
  shared Scoped DbContext **without saving it**.
- Application never references RabbitMQ, EF Core, or the outbox table.

### 3. No unit of work: Enqueue before the saving repository call

Because repositories save by themselves, the rule is: **call
`IOutbox.Enqueue` before the repository call that saves the change.** The
repository's own `SaveChangesAsync` then writes both. Handler unit tests
assert that order (`Received.InOrder`) for every handler that enqueues.

The alternative -- an `IUnitOfWork` that repositories stop saving
through, with handlers calling `SaveChangesAsync` explicitly -- is the
more robust design, but means changing every repository, every write
handler and every handler test. It's deferred rather than rejected: it
becomes worth it the first time one handler has to change more than one
aggregate atomically.

### 4. `OutboxRelay` publishes, in the Worker only

A `BackgroundService` registered by `AddRabbitMqMessaging`, so it runs
only in the Worker. Every 2 seconds it loads up to 50 unprocessed rows,
oldest first, publishes each through `IMessagePublisher` (publisher
confirms, `mandatory` -- ADR 0012), and sets `ProcessedAt` once the broker
has confirmed. The row's `Id` becomes the message's `MessageId`.

- Saved after each message, with `CancellationToken.None`, so a crash or
  shutdown mid-batch doesn't republish messages already confirmed.
- A failed publish increments `Attempts` and records `LastError`. After
  10 failures a row is no longer picked up; it stays in the table for
  inspection -- the outbox's equivalent of a dead-letter queue.
- A missing `OutboxMessages` table is expected briefly at startup (only
  the Api applies migrations, ADR 0008) and is logged as information and
  retried, not treated as an error.

The Api never connects to RabbitMQ at all: it only inserts rows.

### 5. Table design

`Payload` is `jsonb`, so Postgres rejects invalid JSON on insert. A
partial index on `OccurredAt` covering only `ProcessedAt IS NULL` rows
keeps the relay's one query fast however many processed rows accumulate.

## Consequences

**Positive:**

- An event can't be lost between the database and the broker: if the
  change was committed, its event was too, and the relay retries it
  until the broker confirms.
- A RabbitMQ outage no longer affects the Api at all; events queue up in
  Postgres and drain once the broker is back.
- Handlers stay testable with a substituted `IOutbox`, just like
  repositories.

**Negative:**

- **Duplicates are possible** (at-least-once): if the relay stops after
  the broker confirmed a message but before `ProcessedAt` was saved, the
  row is published again. Consumers must be idempotent -- already a rule
  from ADR 0012.
- **Latency**: an event reaches the broker up to one poll interval (2 s)
  after it was committed, plus processing time.
- **The ordering rule is easy to break**: an `Enqueue` placed after the
  save compiles and runs, and the event is silently never written. It is
  guarded by handler tests, not by the type system.
- **Single relay assumed**: two relays (for example, two Worker replicas
  in Kubernetes) could publish the same row twice. Correct given
  idempotent consumers, but wasteful; `SELECT ... FOR UPDATE SKIP LOCKED`
  would fix it if that ever matters.
- **Processed rows are never deleted.** Harmless at this project's scale;
  a periodic cleanup of rows processed more than N days ago would be the
  fix.

## Alternatives Considered

- **Publish directly after saving** -- rejected; that is the dual-write
  problem above.
- **An `IUnitOfWork` refactor now** -- deferred; see decision 3.
- **Postgres `LISTEN/NOTIFY` instead of polling** -- rejected for now.
  Lower latency, but notifications are lost while no listener is
  connected, so a polling fallback would be needed anyway; polling alone
  is simpler and 2 s is fine here.
- **Change data capture (e.g. Debezium reading Postgres's write-ahead
  log)** -- rejected; far too much infrastructure for one Worker.
- **A framework's built-in outbox (MassTransit, Wolverine)** -- not
  available; ADR 0011 chose the raw client.
