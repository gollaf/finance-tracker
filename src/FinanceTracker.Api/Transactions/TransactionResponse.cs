using FinanceTracker.Domain.Transactions;

namespace FinanceTracker.Api.Transactions
{
    /// <summary>Flattens TransactionSummaryDto's Money into Amount/Currency, same reasoning as the other response DTOs.</summary>
    public sealed record TransactionResponse(
        Guid Id,
        Guid AccountId,
        Guid? CategoryId,
        decimal Amount,
        string Currency,
        TransactionType Type,
        string Description,
        DateOnly OccurredOn);
}
