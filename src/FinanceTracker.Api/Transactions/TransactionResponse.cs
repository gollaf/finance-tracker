using FinanceTracker.Domain.Transactions;

namespace FinanceTracker.Api.Transactions
{
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
