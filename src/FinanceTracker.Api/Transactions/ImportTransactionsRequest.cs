namespace FinanceTracker.Api.Transactions
{
    /// <summary>
    /// A class with settable properties, not a record: multipart/form-data
    /// model binding needs setters.
    /// </summary>
    public sealed class ImportTransactionsRequest
    {
        public Guid AccountId { get; set; }

        public IFormFile File { get; set; } = null!;
    }
}
