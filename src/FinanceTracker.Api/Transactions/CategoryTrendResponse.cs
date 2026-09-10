namespace FinanceTracker.Api.Transactions
{
    /// <summary>
    /// Flattens CategoryTrendDto's two Money values into
    /// CurrentMonthTotal/PriorAverageTotal/Currency, same reasoning as
    /// CategorySpendingResponse. CategoryId is null, and CategoryName is
    /// "Uncategorized", for uncategorized spending -- see
    /// GetSpendingInsightsQueryHandler. PercentChange is null when there
    /// was no prior spending in this Category to compare against.
    /// </summary>
    public sealed record CategoryTrendResponse(
        Guid? CategoryId,
        string CategoryName,
        decimal CurrentMonthTotal,
        decimal PriorAverageTotal,
        string Currency,
        decimal? PercentChange);
}
