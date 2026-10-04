namespace FinanceTracker.Api.Transactions
{
    /// <summary>
    /// CategoryId is null for uncategorized spending.
    /// </summary>
    public sealed record CategorySpendingResponse(Guid? CategoryId, decimal Total, string Currency);
}
