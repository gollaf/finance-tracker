using FinanceTracker.Domain.Common;
using FinanceTracker.Domain.Transactions;

namespace FinanceTracker.Application.Transactions
{
    /// <summary>Persistence contract for Transaction, implemented by Infrastructure.</summary>
    public interface ITransactionRepository
    {
        Task<Transaction?> GetByIdAsync(TransactionId id, CancellationToken cancellationToken = default);

        /// <summary>Feeds GetAccountBalance and GetSpendingSummary, which compute rather than store a total.</summary>
        Task<IReadOnlyList<Transaction>> GetByAccountIdAsync(
            AccountId accountId, CancellationToken cancellationToken = default);

        /// <summary>
        /// Feeds GetBudgetStatus. Unfiltered by Account on purpose — a Budget
        /// targets a Category, not an Account, so spending against it has to
        /// be summed across every Account, not just one.
        /// </summary>
        Task<IReadOnlyList<Transaction>> GetByCategoryIdAsync(
            CategoryId categoryId, CancellationToken cancellationToken = default);

        Task AddAsync(Transaction transaction, CancellationToken cancellationToken = default);

        Task UpdateAsync(Transaction transaction, CancellationToken cancellationToken = default);

        Task DeleteAsync(Transaction transaction, CancellationToken cancellationToken = default);
    }
}
