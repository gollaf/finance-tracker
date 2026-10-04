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
    /// EventName is static abstract, so IOutbox.Enqueue reads it from the
    /// type parameter without reflection. The catch: IIntegrationEvent itself
    /// can't be a type argument (CS8920) -- always use the concrete type.
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
