using FinanceTracker.Domain.Common;
using FinanceTracker.Domain.Imports;

namespace FinanceTracker.Application.Imports
{
    /// <summary>
    /// Persistence contract for ImportJob, implemented by Infrastructure.
    /// Reads are scoped to one owner: another user's job is simply not found.
    /// </summary>
    public interface IImportJobRepository
    {
        Task<ImportJob?> GetByIdAsync(UserId ownerId, ImportJobId id, CancellationToken cancellationToken = default);

        Task AddAsync(ImportJob importJob, CancellationToken cancellationToken = default);

        Task UpdateAsync(ImportJob importJob, CancellationToken cancellationToken = default);
    }
}
