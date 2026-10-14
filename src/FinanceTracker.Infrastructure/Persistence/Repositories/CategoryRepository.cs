using FinanceTracker.Application.Categories;
using FinanceTracker.Domain.Categories;
using FinanceTracker.Domain.Common;
using Microsoft.EntityFrameworkCore;

namespace FinanceTracker.Infrastructure.Persistence.Repositories
{
    public sealed class CategoryRepository : ICategoryRepository
    {
        private readonly FinanceTrackerDbContext _context;

        public CategoryRepository(FinanceTrackerDbContext context)
        {
            _context = context;
        }

        public Task<Category?> GetByIdAsync(
            UserId ownerId, CategoryId id, CancellationToken cancellationToken = default) =>
            _context.Categories.SingleOrDefaultAsync(c => c.Id == id && c.OwnerId == ownerId, cancellationToken);

        public Task<bool> ExistsWithNameAsync(
            UserId ownerId, string name, CancellationToken cancellationToken = default) =>
            _context.Categories.AnyAsync(
                c => c.OwnerId == ownerId && EF.Functions.ILike(c.Name, name), cancellationToken);

        public async Task<IReadOnlyList<Category>> GetAllAsync(
            UserId ownerId, CancellationToken cancellationToken = default) =>
            await _context.Categories
                .Where(c => c.OwnerId == ownerId)
                .OrderBy(c => c.Name)
                .ToListAsync(cancellationToken);

        public async Task AddAsync(Category category, CancellationToken cancellationToken = default)
        {
            await _context.Categories.AddAsync(category, cancellationToken);
            await _context.SaveChangesAsync(cancellationToken);
        }
    }
}
