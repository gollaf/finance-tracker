using FinanceTracker.Application.Common;
using FinanceTracker.Domain.Common;
using FinanceTracker.Domain.Imports;
using MediatR;

namespace FinanceTracker.Application.Imports.StartImport
{
    /// <summary>
    /// Accepts already-parsed CSV rows for background import: creates a
    /// Pending ImportJob and requests its processing, and returns the job's
    /// id straight away. Nothing is imported here -- see
    /// ProcessImportJobCommand and docs/adr/0016-asynchronous-csv-import.md.
    /// </summary>
    /// <param name="ParseErrors">Rows the file parser rejected, recorded on the job as-is.</param>
    public sealed record StartImportCommand(
        AccountId AccountId,
        IReadOnlyList<ImportJobRow> Rows,
        IReadOnlyList<ImportJobRowError> ParseErrors) : IRequest<Result<ImportJobId>>;
}
