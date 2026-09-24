using FinanceTracker.Domain.Common;
using FinanceTracker.Domain.Imports;

namespace FinanceTracker.Application.Imports
{
    /// <summary>Persistence contract for ImportJob, implemented by Infrastructure.</summary>
    public interface IImportJobRepository
    {
        Task<ImportJob?> GetByIdAsync(ImportJobId id, CancellationToken cancellationToken = default);

        Task AddAsync(ImportJob importJob, CancellationToken cancellationToken = default);

        Task UpdateAsync(ImportJob importJob, CancellationToken cancellationToken = default);
    }
}
