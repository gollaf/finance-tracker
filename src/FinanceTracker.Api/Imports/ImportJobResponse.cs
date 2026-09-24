using FinanceTracker.Api.Transactions;
using FinanceTracker.Domain.Imports;

namespace FinanceTracker.Api.Imports
{
    /// <summary>
    /// GET /api/imports/{id}. Status is "Pending" until the Worker has
    /// processed the job; ImportedCount and the final Errors are only
    /// meaningful once it is "Completed". While Pending, Errors already
    /// contains the rows that failed to parse at upload.
    /// </summary>
    public sealed record ImportJobResponse(
        Guid Id,
        Guid AccountId,
        ImportJobStatus Status,
        int TotalRows,
        int ImportedCount,
        IReadOnlyList<ImportRowErrorResponse> Errors,
        string? FailureReason,
        DateTimeOffset CreatedAt,
        DateTimeOffset? CompletedAt);
}
