using FinanceTracker.Domain.Categories;
using FinanceTracker.Domain.Common;

namespace FinanceTracker.Application.Categories
{
    /// <summary>Persistence contract for Category, implemented by Infrastructure.</summary>
    public interface ICategoryRepository
    {
        Task<Category?> GetByIdAsync(UserId ownerId, CategoryId id, CancellationToken cancellationToken = default);

        /// <summary>
        /// Backs the name-uniqueness rule Category itself can't enforce (see
        /// docs/domain-model.md). Names are unique per owner: two users can
        /// both have "Groceries".
        /// </summary>
        Task<bool> ExistsWithNameAsync(UserId ownerId, string name, CancellationToken cancellationToken = default);

        /// <summary>
        /// Every Category of one owner, ordered by name. Feeds
        /// SuggestCategoryForTransaction, which offers the AI exactly this list
        /// to choose from.
        /// </summary>
        Task<IReadOnlyList<Category>> GetAllAsync(UserId ownerId, CancellationToken cancellationToken = default);

        Task AddAsync(Category category, CancellationToken cancellationToken = default);
    }
}
