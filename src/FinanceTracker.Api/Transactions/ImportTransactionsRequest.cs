namespace FinanceTracker.Api.Transactions
{
    /// <summary>
    /// The one request DTO in the API that isn't a record -- model binding
    /// for multipart/form-data (needed for a file upload) uses settable
    /// properties, not a record's constructor-based binding used everywhere
    /// else for JSON bodies. AccountId travels in the same form as the file
    /// so both arrive together in one POST.
    /// </summary>
    public sealed class ImportTransactionsRequest
    {
        public Guid AccountId { get; set; }

        public IFormFile File { get; set; } = null!;
    }
}
