using FinanceTracker.Application.Imports;
using FinanceTracker.Domain.Common;
using FinanceTracker.Domain.Imports;
using Microsoft.EntityFrameworkCore;

namespace FinanceTracker.Infrastructure.Persistence.Repositories
{
    public sealed class ImportJobRepository : IImportJobRepository
    {
        private readonly FinanceTrackerDbContext _context;

        public ImportJobRepository(FinanceTrackerDbContext context)
        {
            _context = context;
        }

        public Task<ImportJob?> GetByIdAsync(ImportJobId id, CancellationToken cancellationToken = default) =>
            _context.ImportJobs.SingleOrDefaultAsync(j => j.Id == id, cancellationToken);

        public async Task AddAsync(ImportJob importJob, CancellationToken cancellationToken = default)
        {
            await _context.ImportJobs.AddAsync(importJob, cancellationToken);
            await _context.SaveChangesAsync(cancellationToken);
        }

        public async Task UpdateAsync(ImportJob importJob, CancellationToken cancellationToken = default)
        {
            _context.ImportJobs.Update(importJob);
            await _context.SaveChangesAsync(cancellationToken);
        }
    }
}
