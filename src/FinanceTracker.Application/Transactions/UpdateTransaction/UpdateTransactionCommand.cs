using FinanceTracker.Application.Common;
using FinanceTracker.Domain.Common;
using MediatR;

namespace FinanceTracker.Application.Transactions.UpdateTransaction
{
    /// <summary>
    /// Replaces a Transaction's editable fields: Amount, Description, and
    /// OccurredOn. Type isn't editable — Transaction never exposes a way to
    /// change Income/Expense after creation (see Transaction.cs); if the
    /// direction was wrong, delete and re-add instead. Category isn't
    /// touched here either — that's CategorizeTransaction's job.
    /// </summary>
    public sealed record UpdateTransactionCommand(
        TransactionId TransactionId,
        decimal Amount,
        string Description,
        DateOnly OccurredOn) : IRequest<Result>;
}
