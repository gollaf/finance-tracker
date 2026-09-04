using FinanceTracker.Application.Categories;
using FinanceTracker.Domain.Categories;
using FinanceTracker.Domain.Common;
using Microsoft.EntityFrameworkCore;

namespace FinanceTracker.Infrastructure.Persistence.Repositories
{
    /// <summary>
    /// EF Core implementation of ICategoryRepository. Each mutating method
    /// calls SaveChangesAsync itself — there's no separate unit-of-work
    /// abstraction, matching how Application's handlers already call
    /// repositories (no cross-repository transaction is needed yet; see
    /// ADR 0002 on why Phase 1 has no cross-aggregate consistency problem
    /// to solve in the first place).
    /// </summary>
    public sealed class CategoryRepository : ICategoryRepository
    {
        private readonly FinanceTrackerDbContext _context;

        public CategoryRepository(FinanceTrackerDbContext context)
        {
            _context = context;
        }

        public Task<Category?> GetByIdAsync(CategoryId id, CancellationToken cancellationToken = default) =>
            _context.Categories.SingleOrDefaultAsync(c => c.Id == id, cancellationToken);

        public Task<bool> ExistsWithNameAsync(string name, CancellationToken cancellationToken = default) =>
            _context.Categories.AnyAsync(c => EF.Functions.ILike(c.Name, name), cancellationToken);

        public async Task AddAsync(Category category, CancellationToken cancellationToken = default)
        {
            await _context.Categories.AddAsync(category, cancellationToken);
            await _context.SaveChangesAsync(cancellationToken);
        }
    }
}
