using FinanceTracker.Domain.Common;

namespace FinanceTracker.Application.Transactions.GetSpendingInsights
{
    /// <summary>
    /// One Category's Expense total for the requested month next to its
    /// average over the three preceding calendar months. CategoryId is null,
    /// and CategoryName is "Uncategorized", for spending with no Category.
    /// PercentChange is null when the prior average is zero, since there is
    /// no meaningful percentage change from a zero baseline.
    /// </summary>
    public sealed record CategoryTrendDto(
        CategoryId? CategoryId,
        string CategoryName,
        Money CurrentMonthTotal,
        Money PriorAverageTotal,
        decimal? PercentChange);
}
