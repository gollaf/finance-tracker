# 38. Trace context through the outbox

## Status

Accepted. Amends [ADR 0037](./0037-telemetry-with-opentelemetry.md).

## Context

ADR 0037 traces requests, database calls, and RabbitMQ publishing and
consuming. A trace still stops at the outbox
([ADR 0013](./0013-transactional-outbox.md)): a request only writes an
event to the `OutboxMessages` table, and the relay publishes it later, in
the Worker, inside a polling loop that belongs to no request. RabbitMQ.Client
makes its publish span a child of whatever span is current, so the publish
started a new trace each time, and the consumer's spans joined that new
trace. Adding a transaction showed up as two unrelated traces: the request,
and the categorization.

A span is identified across processes by a W3C traceparent,
`00-{trace id}-{parent span id}-{flags}`. HTTP carries it in a header of
that name, and RabbitMQ.Client in a message header. The outbox is the one
hop with no place for it.

## Decision

1. **The outbox row stores the traceparent** of the span current when the
   event is enqueued -- the request's, or, for events recorded while the
   Worker handles a message, that message's span. New nullable column
   `OutboxMessages.TraceParent` (at most 55 characters, the length of a
   version 00 traceparent). Null when nothing is traced or the current span
   uses .NET's older, non-W3C ID format.
2. **The relay publishes inside a span that continues that trace.** A new
   ActivitySource, `FinanceTracker.Outbox`, starts an `outbox relay
   {event name}` span per message, with the stored traceparent as its
   remote parent. RabbitMQ.Client's publish span becomes its child and is
   written into the message's headers. The span records the message ID,
   the attempt number, and how long the event waited in the table
   (`outbox.waited_ms`); a failed publish marks it as an error. The save
   that records the result happens after the span has ended.
3. **The consumer side needs no change.** RabbitMQ.Client starts a consumer
   span around each delivery, as a child of the publish span by default
   (`RabbitMQTracingOptions.UsePublisherAsParent`), and the handler -- its
   database calls and its Groq call -- runs inside it. A test pins this
   behaviour, so a client upgrade that changed it fails.
4. **Parent, not link.** OpenTelemetry also allows linking the publish to
   the earlier trace instead of continuing it. A parent keeps the whole
   story in one trace, which is what someone following one request wants.

## Consequences

**Positive:**

- One trace runs from the HTTP request through the outbox, the broker and
  the consumer, across both processes. A CSV import shows every row's
  categorization in the import request's trace, two outbox hops later.
- The wait in the outbox is visible: a gap in the timeline, and a number on
  the relay's span.
- Logs written while publishing or handling a message carry the request's
  trace ID.

**Negative:**

- **A trace's duration includes the outbox wait**, up to the relay's poll
  interval, and every retry of a failed publish adds a span to the original
  trace, possibly minutes later.
- **One more column** on every outbox row, filled even when no telemetry
  backend is configured (the ID costs nothing to generate).
- **Rows enqueued before this change have no traceparent**; they publish in
  a trace of their own, as before.
- **Sampling follows the request.** If the request's span was not recorded,
  the relay's and consumer's spans aren't either.

## Alternatives Considered

- **Span links instead of a parent** -- the semantic conventions' choice
  for batches, but the request and its consequences would stay separate
  traces, joined only by a reference.
- **Storing the traceparent inside the event's JSON** -- mixes transport
  metadata into the integration event contracts (ADR 0015) that consumers
  deserialize.
- **Also storing `tracestate`** -- vendor-specific trace data; nothing in
  this system sets it.
- **Publishing directly from the request** -- what the outbox exists to
  avoid.
