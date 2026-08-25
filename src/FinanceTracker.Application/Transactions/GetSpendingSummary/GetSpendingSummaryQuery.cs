using FinanceTracker.Application.Common;
using FinanceTracker.Domain.Common;
using MediatR;

namespace FinanceTracker.Application.Transactions.GetSpendingSummary
{
    /// <summary>
    /// Groups one Account's Expense Transactions by Category for one month.
    /// Income isn't included — this answers "where did the money go," which
    /// GetAccountBalance's net figure doesn't show.
    /// </summary>
    public sealed record GetSpendingSummaryQuery(AccountId AccountId, int Year, int Month)
        : IRequest<Result<IReadOnlyList<CategorySpendingDto>>>;
}
