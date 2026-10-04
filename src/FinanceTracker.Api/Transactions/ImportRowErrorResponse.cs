namespace FinanceTracker.Api.Transactions
{
    /// <summary>
    /// RowNumber is the line in the CSV file: the header is line 1, so the
    /// first data row is line 2.
    /// </summary>
    public sealed record ImportRowErrorResponse(int RowNumber, string Message);
}
