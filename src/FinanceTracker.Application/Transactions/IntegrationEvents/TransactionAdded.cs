using FinanceTracker.Application.Common.IntegrationEvents;

namespace FinanceTracker.Application.Transactions.IntegrationEvents
{
    /// <summary>
    /// A Transaction was added -- manually or by CSV import, categorized by a
    /// rule or not. Published for every new Transaction: it states a fact,
    /// and each consumer decides for itself whether that fact concerns it
    /// (the Worker's categorization consumer, for one, skips Transactions
    /// that already have a Category). See
    /// docs/adr/0015-integration-event-contracts.md.
    /// </summary>
    /// <param name="TransactionId">
    /// A plain Guid, not the TransactionId domain type: this record is a
    /// wire contract between processes, serialized to JSON, and must not
    /// change shape just because a domain type's internals do.
    /// </param>
    public sealed record TransactionAdded(Guid TransactionId) : IIntegrationEvent
    {
        public static string EventName => "transaction.added";
    }
}
