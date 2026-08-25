using FinanceTracker.Domain.Common;

namespace FinanceTracker.Application.Transactions.ImportTransactionsFromCsv
{
    /// <summary>
    /// Partial-success outcome of an import: rows that succeeded and rows
    /// that didn't are both reported — one bad row never fails the whole
    /// batch (see ImportTransactionsFromCsvCommandHandler).
    /// </summary>
    public sealed record ImportTransactionsResult(
        IReadOnlyList<TransactionId> ImportedTransactionIds,
        IReadOnlyList<ImportRowError> Errors);
}
