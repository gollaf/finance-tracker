using FinanceTracker.Domain.Common;

namespace FinanceTracker.Application.Transactions.GetSpendingInsights
{
    /// <summary>
    /// One Category's Expense total for the requested month next to the
    /// average of the same Category across the three preceding calendar
    /// months. CategoryId is null, and CategoryName is "Uncategorized", for
    /// spending with no Category assigned -- same convention
    /// GetSpendingSummary's CategorySpendingDto uses for the id, extended
    /// with a resolved name here because both the AI prompt and the
    /// fallback narrative need something more readable than a CategoryId to
    /// talk about. PercentChange is null when PriorAverage is zero -- there
    /// is no meaningful percentage change from a zero baseline, and
    /// computing one would either divide by zero or report a misleading
    /// "infinite" increase.
    /// </summary>
    public sealed record CategoryTrendDto(
        CategoryId? CategoryId,
        string CategoryName,
        Money CurrentMonthTotal,
        Money PriorAverageTotal,
        decimal? PercentChange);
}
