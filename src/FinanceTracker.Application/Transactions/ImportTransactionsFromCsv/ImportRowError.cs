namespace FinanceTracker.Application.Transactions.ImportTransactionsFromCsv
{
    /// <summary>One row that failed to import, with its position in the batch.</summary>
    public sealed record ImportRowError(int RowIndex, string Message);
}
