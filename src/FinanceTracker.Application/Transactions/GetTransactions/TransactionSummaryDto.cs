using FinanceTracker.Domain.Common;
using FinanceTracker.Domain.Transactions;

namespace FinanceTracker.Application.Transactions.GetTransactions
{
    /// <summary>Read-only projection of a Transaction — queries return this, never the aggregate itself.</summary>
    public sealed record TransactionSummaryDto(
        TransactionId Id,
        AccountId AccountId,
        CategoryId? CategoryId,
        Money Amount,
        TransactionType Type,
        string Description,
        DateOnly OccurredOn);
}
