# 15. Integration event contracts

## Status

Accepted

## Context

With the outbox (ADR 0013), the RabbitMQ plumbing (ADR 0012), and the
categorization command (ADR 0014) in place, the Api and the Worker now
communicate through their first integration event. An integration event
is a contract between two separately deployed processes: once one version
of the Api is writing it and another version of the Worker is reading it,
changing its shape is no longer a refactoring inside one codebase. A few
rules are worth fixing before there are several events.

## Decision

1. **Events state facts, named in the past tense.** The first one is
   `TransactionAdded`, published for **every** new Transaction -- whether
   added by hand or imported, and whether or not a rule already
   categorized it. The publisher doesn't decide what consumers need;
   each consumer filters for itself. The categorization consumer skips
   Transactions that already have a Category (its handler's first
   check), and a future consumer, such as budget alerts, can subscribe to
   the same event without the publisher changing.
2. **Primitive types only in event payloads.** `TransactionAdded` carries
   a plain `Guid`, not the `TransactionId` domain type. The payload is
   JSON on the wire, and how a domain type serializes (`{"value": ...}`
   versus a bare string) is an accident of its current implementation
   that the other process shouldn't depend on.
3. **Minimal payloads.** Ids, not copies of entities: the consumer loads
   current state from the database when it runs. That also means a
   consumer always acts on the *current* Transaction, not on a snapshot
   that may be stale by the time the message is processed.
4. **Event names are dot-separated and stable** (`"transaction.added"`),
   and double as the routing key (ADR 0013). Renaming one silently
   disconnects every queue bound to the old name, so an event's name is
   never changed; a genuinely different event gets a new name.
5. **One queue per consumer, named for what the consumer does**
   (`finance-tracker.categorize-transaction`), not for the event. Each
   consumer gets its own copy of every event, and its own retry count and
   dead-letter queue.
6. **Consumers are thin inbound adapters in the Worker**, the messaging
   equivalent of Api controllers: deserialize, send one MediatR command,
   log the outcome. Business logic stays in Application handlers.

## Consequences

**Positive:**

- New consumers can be added without touching the code that publishes.
- A payload of ids can't go stale or leak more data than needed.
- Worker and Api can evolve their domain types independently of the
  wire format.

**Negative:**

- Publishing for every Transaction sends messages some consumers only
  skip -- one cheap database read each for the categorization consumer.
  Accepted in exchange for the publisher not knowing its consumers.
- An id-only event forces every consumer to read the database, so a
  consumer can't run if the database is down (it's retried instead).
- Evolving a payload later needs care: fields can be added (older
  consumers ignore unknown JSON properties), but removing or renaming one
  requires a new event name or a coordinated deployment.

## Alternatives Considered

- **A command-style message ("CategorizeTransaction") sent only for
  uncategorized Transactions** -- rejected; it would couple the publisher
  to one specific consumer, and every future reaction to a new Transaction
  would need the publisher changed again.
- **Domain types in payloads** -- rejected; see decision 2.
- **Full Transaction snapshots in the payload** -- rejected; more data on
  the wire and in the outbox, and a snapshot that's stale by the time it
  is processed.
