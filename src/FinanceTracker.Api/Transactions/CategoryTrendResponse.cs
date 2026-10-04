namespace FinanceTracker.Api.Transactions
{
    /// <summary>
    /// CategoryId is null, and CategoryName is "Uncategorized", for
    /// uncategorized spending. PercentChange is null when there was no prior
    /// spending in the Category to compare against.
    /// </summary>
    public sealed record CategoryTrendResponse(
        Guid? CategoryId,
        string CategoryName,
        decimal CurrentMonthTotal,
        decimal PriorAverageTotal,
        string Currency,
        decimal? PercentChange);
}
