using FinanceTracker.Application.Common;
using FinanceTracker.Domain.Common;
using MediatR;

namespace FinanceTracker.Application.Transactions.CategorizeTransaction
{
    /// <summary>
    /// Assigns or clears a Transaction's Category. CategoryId is nullable —
    /// passing null uncategorizes the transaction, mirroring
    /// Transaction.Recategorize's own signature.
    /// </summary>
    public sealed record CategorizeTransactionCommand(TransactionId TransactionId, CategoryId? CategoryId)
        : IRequest<Result>;
}
