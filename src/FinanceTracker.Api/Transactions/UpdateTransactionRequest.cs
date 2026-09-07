namespace FinanceTracker.Api.Transactions
{
    public sealed record UpdateTransactionRequest(decimal Amount, string Description, DateOnly OccurredOn);
}
