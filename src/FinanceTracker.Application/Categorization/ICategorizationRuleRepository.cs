using FinanceTracker.Domain.Categorization;
using FinanceTracker.Domain.Common;

namespace FinanceTracker.Application.Categorization
{
    /// <summary>
    /// Persistence contract for CategorizationRule, implemented by
    /// Infrastructure. GetAllAsync feeds directly into
    /// TransactionCategorizer.Categorize.
    /// </summary>
    public interface ICategorizationRuleRepository
    {
        Task<IReadOnlyList<CategorizationRule>> GetAllAsync(
            UserId ownerId, CancellationToken cancellationToken = default);

        Task AddAsync(CategorizationRule rule, CancellationToken cancellationToken = default);
    }
}
