using FinanceTracker.Application.Common;
using FinanceTracker.Domain.Common;
using MediatR;

namespace FinanceTracker.Application.Transactions.GetTransactions
{
    /// <summary>
    /// Lists an Account's Transactions, most recent first. From/To are both
    /// optional and inclusive — omit either to leave that side of the range
    /// unbounded.
    /// </summary>
    public sealed record GetTransactionsQuery(AccountId AccountId, DateOnly? From = null, DateOnly? To = null)
        : IRequest<Result<IReadOnlyList<TransactionSummaryDto>>>;
}
