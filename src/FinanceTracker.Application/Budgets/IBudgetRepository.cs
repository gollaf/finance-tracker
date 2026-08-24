using FinanceTracker.Domain.Budgets;
using FinanceTracker.Domain.Common;

namespace FinanceTracker.Application.Budgets
{
    /// <summary>Persistence contract for Budget, implemented by Infrastructure.</summary>
    public interface IBudgetRepository
    {
        Task<Budget?> GetByIdAsync(BudgetId id, CancellationToken cancellationToken = default);

        /// <summary>Backs the one-budget-per-category-per-period rule (see docs/domain-model.md).</summary>
        Task<Budget?> GetByCategoryAndPeriodAsync(
            CategoryId categoryId, BudgetPeriod period, CancellationToken cancellationToken = default);

        Task AddAsync(Budget budget, CancellationToken cancellationToken = default);

        Task UpdateAsync(Budget budget, CancellationToken cancellationToken = default);
    }
}
