namespace FinanceTracker.Api.Transactions
{
    /// <summary>
    /// A partial-success shape, same as ImportTransactionsResult underneath
    /// it -- a batch import with some bad rows is still a 200, not a
    /// failure, with the good rows imported and the bad ones listed here.
    /// </summary>
    public sealed record ImportTransactionsResponse(
        IReadOnlyList<Guid> ImportedTransactionIds, IReadOnlyList<ImportRowErrorResponse> Errors);
}
