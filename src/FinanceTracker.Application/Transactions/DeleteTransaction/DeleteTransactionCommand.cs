using FinanceTracker.Application.Common;
using FinanceTracker.Domain.Common;
using MediatR;

namespace FinanceTracker.Application.Transactions.DeleteTransaction
{
    /// <summary>
    /// Hard-deletes a Transaction. There's no soft-delete or undo — Transaction
    /// carries no "deleted" flag, so once this runs the row is gone.
    /// </summary>
    public sealed record DeleteTransactionCommand(TransactionId TransactionId) : IRequest<Result>;
}
