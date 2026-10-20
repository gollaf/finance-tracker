namespace FinanceTracker.Infrastructure.Outbox
{
    /// <summary>
    /// One row of the transactional outbox: an integration event waiting to
    /// be (or already) published to RabbitMQ. See
    /// docs/adr/0013-transactional-outbox.md.
    /// </summary>
    public sealed class OutboxMessage
    {
        public const int MaxTypeLength = 200;
        public const int MaxEventNameLength = 200;
        public const int MaxLastErrorLength = 2000;

        /// <summary>A W3C traceparent of version 00 is always exactly this long.</summary>
        public const int MaxTraceParentLength = 55;

        public OutboxMessage(
            Guid id, string type, string eventName, string payload, DateTimeOffset occurredAt, string? traceParent = null)
        {
            Id = id;
            Type = type;
            EventName = eventName;
            Payload = payload;
            OccurredAt = occurredAt;
            TraceParent = traceParent;
        }

        /// <summary>
        /// Also the published message's MessageId, so it stays the same no
        /// matter how many times the relay has to publish this row.
        /// </summary>
        public Guid Id { get; private set; }

        /// <summary>The event's CLR type name, e.g. "TransactionAdded" -- for humans and logs.</summary>
        public string Type { get; private set; }

        /// <summary>IIntegrationEvent.EventName; published as the routing key.</summary>
        public string EventName { get; private set; }

        /// <summary>The event serialized as JSON.</summary>
        public string Payload { get; private set; }

        public DateTimeOffset OccurredAt { get; private set; }

        /// <summary>
        /// The span the event was recorded in, as a W3C traceparent
        /// ("00-{trace id}-{span id}-{flags}"), so the relay can publish it
        /// as part of the same trace. Null when nothing was being traced.
        /// </summary>
        public string? TraceParent { get; private set; }

        /// <summary>Null until the broker has confirmed receiving it.</summary>
        public DateTimeOffset? ProcessedAt { get; private set; }

        /// <summary>Failed publish attempts so far; see OutboxRelay.MaxAttempts.</summary>
        public int Attempts { get; private set; }

        public string? LastError { get; private set; }

        public void MarkProcessed(DateTimeOffset processedAt)
        {
            ProcessedAt = processedAt;
            LastError = null;
        }

        public void RecordFailure(string error)
        {
            Attempts++;
            LastError = error.Length <= MaxLastErrorLength ? error : error[..MaxLastErrorLength];
        }
    }
}
