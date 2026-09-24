namespace FinanceTracker.Application.Common.IntegrationEvents
{
    /// <summary>
    /// Port for recording that an integration event happened, so it is
    /// published to the message broker *eventually* -- never directly, and
    /// never lost. Implemented by Infrastructure as the transactional outbox:
    /// the event is stored as a row in the same database transaction as the
    /// change that caused it, and published afterwards by a separate relay.
    /// See docs/adr/0013-transactional-outbox.md.
    /// </summary>
    /// <remarks>
    /// Ordering rule: call Enqueue BEFORE the repository call that saves the
    /// change the event describes. Enqueue only stages the event; it is
    /// written to the database by the next save, together with that change,
    /// in one transaction. Enqueuing after the save has already happened
    /// means nothing ever writes the event.
    /// </remarks>
    public interface IOutbox
    {
        void Enqueue<TEvent>(TEvent integrationEvent)
            where TEvent : IIntegrationEvent;
    }
}
