namespace FinanceTracker.Infrastructure.Messaging
{
    /// <summary>
    /// One message ready to publish: the body is already-serialized JSON
    /// (see MessageSerialization), not a typed object, because the publisher
    /// never needs to know the CLR type -- only where to send the bytes. That
    /// shape also matches what a message looks like after being stored and
    /// read back as a row, rather than living in memory as an object.
    /// </summary>
    /// <param name="MessageId">
    /// Unique per logical message and stable across re-sends, so a consumer
    /// can recognize a duplicate delivery of the same message.
    /// </param>
    /// <param name="Type">A human-readable message type name, e.g. "TransactionAdded".</param>
    /// <param name="RoutingKey">Decides which queues the events exchange copies it into.</param>
    /// <param name="Body">JSON payload.</param>
    public sealed record OutgoingMessage(Guid MessageId, string Type, string RoutingKey, string Body);
}
