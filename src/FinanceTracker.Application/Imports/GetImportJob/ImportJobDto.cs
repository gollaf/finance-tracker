using FinanceTracker.Domain.Common;
using FinanceTracker.Domain.Imports;

namespace FinanceTracker.Application.Imports.GetImportJob
{
    /// <summary>
    /// An ImportJob without its raw Rows -- the client already has its own
    /// file; it needs the outcome, not its rows sent back.
    /// </summary>
    public sealed record ImportJobDto(
        ImportJobId Id,
        AccountId AccountId,
        ImportJobStatus Status,
        int TotalRows,
        int ImportedCount,
        IReadOnlyList<ImportJobRowError> Errors,
        string? FailureReason,
        DateTimeOffset CreatedAt,
        DateTimeOffset? CompletedAt);
}
