namespace FinanceTracker.Infrastructure.Messaging
{
    /// <summary>
    /// One message ready to publish. The body is already-serialized JSON
    /// (see MessageSerialization): the publisher never needs the CLR type,
    /// and an outbox row stores the event in exactly this form.
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
