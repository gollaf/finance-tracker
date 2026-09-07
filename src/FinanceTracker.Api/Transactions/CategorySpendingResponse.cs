namespace FinanceTracker.Api.Transactions
{
    /// <summary>
    /// Flattens CategorySpendingDto's Money into Total/Currency, same
    /// reasoning as the other response DTOs. CategoryId is null for
    /// uncategorized spending -- that spending still happened and isn't
    /// dropped from the summary, see GetSpendingSummaryQueryHandler.
    /// </summary>
    public sealed record CategorySpendingResponse(Guid? CategoryId, decimal Total, string Currency);
}
