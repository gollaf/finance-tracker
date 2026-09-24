using FinanceTracker.Application.Common;
using FinanceTracker.Domain.Common;
using MediatR;

namespace FinanceTracker.Application.Imports.ProcessImport
{
    /// <summary>
    /// Imports a Pending ImportJob's rows as Transactions and records the
    /// outcome on the job -- all in ONE database transaction (IUnitOfWork),
    /// so a crash at any point leaves either everything or nothing, and a
    /// redelivered message can never import a row twice. Sent by the Worker
    /// in response to an ImportRequested event. See
    /// docs/adr/0016-asynchronous-csv-import.md.
    /// </summary>
    public sealed record ProcessImportJobCommand(ImportJobId ImportJobId)
        : IRequest<Result<ProcessImportJobOutcome>>;
}
