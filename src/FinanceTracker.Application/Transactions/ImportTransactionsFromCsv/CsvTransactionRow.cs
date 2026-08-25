using FinanceTracker.Domain.Transactions;

namespace FinanceTracker.Application.Transactions.ImportTransactionsFromCsv
{
    /// <summary>
    /// One already-parsed row from an imported statement. Reading and
    /// parsing the actual CSV file — splitting columns, handling quoting —
    /// is I/O and belongs to Infrastructure/API once that layer exists;
    /// this command only turns structured rows into Transactions.
    /// </summary>
    public sealed record CsvTransactionRow(
        decimal Amount,
        TransactionType Type,
        string Description,
        DateOnly OccurredOn);
}
