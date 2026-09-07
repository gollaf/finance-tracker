namespace FinanceTracker.Api.Transactions
{
    /// <summary>CategoryId is nullable -- omit it or send null to clear the Transaction's category.</summary>
    public sealed record CategorizeTransactionRequest(Guid? CategoryId);
}
