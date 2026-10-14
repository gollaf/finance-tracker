using FinanceTracker.Application.Common;
using FinanceTracker.Domain.Common;
using MediatR;

namespace FinanceTracker.Application.Imports.GetImportJob
{
    /// <summary>The current state of one ImportJob -- what a client polls after starting an import.</summary>
    public sealed record GetImportJobQuery(UserId OwnerId, ImportJobId ImportJobId) : IRequest<Result<ImportJobDto>>;
}
