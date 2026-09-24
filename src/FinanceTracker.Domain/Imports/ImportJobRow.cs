using FinanceTracker.Domain.Transactions;

namespace FinanceTracker.Domain.Imports
{
    /// <summary>
    /// One already-parsed row waiting to be imported. RowNumber is the line
    /// in the uploaded file (the header is line 1), so an error about this
    /// row can be reported in terms the user can find in their own file.
    /// </summary>
    public sealed record ImportJobRow(
        int RowNumber, decimal Amount, TransactionType Type, string Description, DateOnly OccurredOn);
}
