using FinanceTracker.Domain.Categories;
using FinanceTracker.Domain.Common;

namespace FinanceTracker.Application.Categories
{
    /// <summary>Persistence contract for Category, implemented by Infrastructure.</summary>
    public interface ICategoryRepository
    {
        Task<Category?> GetByIdAsync(CategoryId id, CancellationToken cancellationToken = default);

        /// <summary>Backs the name-uniqueness rule Category itself can't enforce (see docs/domain-model.md).</summary>
        Task<bool> ExistsWithNameAsync(string name, CancellationToken cancellationToken = default);

        Task AddAsync(Category category, CancellationToken cancellationToken = default);
    }
}
