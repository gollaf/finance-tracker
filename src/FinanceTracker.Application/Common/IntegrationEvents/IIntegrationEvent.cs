namespace FinanceTracker.Application.Common.IntegrationEvents
{
    /// <summary>
    /// A fact about something that already happened in this application,
    /// published so that work in *another process* (the Worker) can react to
    /// it -- as opposed to a domain event, which stays inside one process.
    /// Named in the past tense ("TransactionAdded"), and kept small: ids and
    /// the few values a consumer needs, never a whole entity.
    /// </summary>
    /// <remarks>
    /// EventName is a <c>static abstract</c> member (C# 11): every event type
    /// must declare its own name, and IOutbox.Enqueue reads it straight from
    /// the type parameter (<c>TEvent.EventName</c>) with no instance
    /// property to serialize and no reflection. One consequence of static
    /// abstract members: IIntegrationEvent itself can't be used as a type
    /// argument (<c>List&lt;IIntegrationEvent&gt;</c> is a compile error,
    /// CS8920) -- always use the concrete event type.
    /// </remarks>
    public interface IIntegrationEvent
    {
        /// <summary>
        /// Stable, dot-separated name, e.g. "transaction.added". Used as the
        /// message's routing key, so consumers subscribe by this name --
        /// changing it for an existing event silently disconnects every
        /// consumer bound to the old one.
        /// </summary>
        static abstract string EventName { get; }
    }
}
