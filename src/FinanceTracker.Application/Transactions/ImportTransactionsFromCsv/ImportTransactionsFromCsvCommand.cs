using FinanceTracker.Application.Common;
using FinanceTracker.Domain.Common;
using MediatR;

namespace FinanceTracker.Application.Transactions.ImportTransactionsFromCsv
{
    /// <summary>
    /// Imports a batch of already-parsed rows into one Account. Categories
    /// aren't supplied per row — each row is run through
    /// TransactionCategorizer against the existing CategorizationRules, the
    /// same rule-matching CategorizeTransaction otherwise leaves to a
    /// separate, explicit call.
    /// </summary>
    public sealed record ImportTransactionsFromCsvCommand(AccountId AccountId, IReadOnlyList<CsvTransactionRow> Rows)
        : IRequest<Result<ImportTransactionsResult>>;
}
