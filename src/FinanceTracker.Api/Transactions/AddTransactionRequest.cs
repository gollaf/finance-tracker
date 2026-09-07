using FinanceTracker.Domain.Transactions;

namespace FinanceTracker.Api.Transactions
{
    public sealed record AddTransactionRequest(
        Guid AccountId, decimal Amount, TransactionType Type, string Description, DateOnly OccurredOn);
}
