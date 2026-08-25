using FinanceTracker.Domain.Common;

namespace FinanceTracker.Application.Transactions.GetSpendingSummary
{
    /// <summary>
    /// Total Expense amount for one Category within a period. CategoryId is
    /// null for uncategorized spending — that spending still happened and
    /// isn't dropped from the summary.
    /// </summary>
    public sealed record CategorySpendingDto(CategoryId? CategoryId, Money Total);
}
