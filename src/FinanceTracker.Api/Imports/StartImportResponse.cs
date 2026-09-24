namespace FinanceTracker.Api.Imports
{
    /// <summary>
    /// The body of 202 Accepted from POST /api/transactions/import -- just the
    /// job to poll. The same URL is also in the response's Location header.
    /// </summary>
    public sealed record StartImportResponse(Guid ImportJobId);
}
