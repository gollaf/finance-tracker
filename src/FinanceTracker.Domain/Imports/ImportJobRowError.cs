namespace FinanceTracker.Domain.Imports
{
    /// <summary>A row that was not imported, by its line number in the uploaded file.</summary>
    public sealed record ImportJobRowError(int RowNumber, string Message);
}
