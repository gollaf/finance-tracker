using FinanceTracker.Application.Common;
using FinanceTracker.Domain.Common;
using FinanceTracker.Domain.Transactions;
using MediatR;

namespace FinanceTracker.Application.Transactions.AddTransaction
{
    /// <summary>
    /// Records a new, uncategorized Transaction against an existing Account.
    /// No Currency here by design — the handler takes it from the Account it
    /// loads, so a transaction can never end up in a different currency than
    /// the account it belongs to. Categorizing it is a separate step; see
    /// CategorizeTransaction.
    /// </summary>
    public sealed record AddTransactionCommand(
        AccountId AccountId,
        decimal Amount,
        TransactionType Type,
        string Description,
        DateOnly OccurredOn) : IRequest<Result<TransactionId>>;
}
