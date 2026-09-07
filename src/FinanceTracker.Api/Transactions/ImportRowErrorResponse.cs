namespace FinanceTracker.Api.Transactions
{
    /// <summary>
    /// RowNumber is the CSV file's own physical line number (header is line
    /// 1, so the first data row is line 2) -- convenient for opening the
    /// file and jumping straight to the bad line, unlike a 0-based index
    /// into the data rows.
    /// </summary>
    public sealed record ImportRowErrorResponse(int RowNumber, string Message);
}
