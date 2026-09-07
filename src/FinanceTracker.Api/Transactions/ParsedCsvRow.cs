using FinanceTracker.Application.Transactions.ImportTransactionsFromCsv;

namespace FinanceTracker.Api.Transactions
{
    /// <summary>
    /// A successfully-parsed CSV row paired with its original position in
    /// the file. OriginalIndex lets the controller translate a RowIndex the
    /// command hands back (relative to the filtered list of good rows it
    /// was given) back into that row's real place in the uploaded file.
    /// </summary>
    internal sealed record ParsedCsvRow(CsvTransactionRow Row, int OriginalIndex);
}
