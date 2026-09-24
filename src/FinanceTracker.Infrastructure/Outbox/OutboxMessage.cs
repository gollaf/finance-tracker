namespace FinanceTracker.Infrastructure.Outbox
{
    /// <summary>
    /// One row of the transactional outbox: an integration event waiting to
    /// be (or already) published to RabbitMQ. A persistence concern, not a
    /// business concept -- which is why it lives in Infrastructure rather
    /// than Domain. See docs/adr/0013-transactional-outbox.md.
    /// </summary>
    public sealed class OutboxMessage
    {
        public const int MaxTypeLength = 200;
        public const int MaxEventNameLength = 200;
        public const int MaxLastErrorLength = 2000;

        public OutboxMessage(Guid id, string type, string eventName, string payload, DateTimeOffset occurredAt)
        {
            Id = id;
            Type = type;
            EventName = eventName;
            Payload = payload;
            OccurredAt = occurredAt;
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
