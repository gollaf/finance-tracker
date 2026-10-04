using FinanceTracker.Application.Transactions;
using FinanceTracker.Domain.Common;
using FinanceTracker.Domain.Transactions;
using Microsoft.EntityFrameworkCore;

namespace FinanceTracker.Infrastructure.Persistence.Repositories
{
    public sealed class TransactionRepository : ITransactionRepository
    {
        private readonly FinanceTrackerDbContext _context;

        public TransactionRepository(FinanceTrackerDbContext context)
        {
            _context = context;
        }

        public Task<Transaction?> GetByIdAsync(TransactionId id, CancellationToken cancellationToken = default) =>
            _context.Transactions.SingleOrDefaultAsync(t => t.Id == id, cancellationToken);

        public async Task<IReadOnlyList<Transaction>> GetByAccountIdAsync(
            AccountId accountId, CancellationToken cancellationToken = default) =>
            await _context.Transactions
                .Where(t => t.AccountId == accountId)
                .ToListAsync(cancellationToken);

        public async Task<IReadOnlyList<Transaction>> GetByCategoryIdAsync(
            CategoryId categoryId, CancellationToken cancellationToken = default) =>
            await _context.Transactions
                .Where(t => t.CategoryId == categoryId)
                .ToListAsync(cancellationToken);

        public async Task AddAsync(Transaction transaction, CancellationToken cancellationToken = default)
        {
            await _context.Transactions.AddAsync(transaction, cancellationToken);
            await _context.SaveChangesAsync(cancellationToken);
        }

        public async Task UpdateAsync(Transaction transaction, CancellationToken cancellationToken = default)
        {
            _context.Transactions.Update(transaction);
            await _context.SaveChangesAsync(cancellationToken);
        }

        // One UPDATE ... WHERE "CategoryId" IS NULL, so the check and the
        // write are atomic. It bypasses the change tracker: an instance this
        // DbContext already loaded still shows the old value.
        public async Task<bool> TrySetCategoryIfUncategorizedAsync(
            TransactionId transactionId, CategoryId categoryId, CancellationToken cancellationToken = default)
        {
            var rowsAffected = await _context.Transactions
                .Where(t => t.Id == transactionId && t.CategoryId == null)
                .ExecuteUpdateAsync(
                    setters => setters.SetProperty(t => t.CategoryId, (CategoryId?)categoryId),
                    cancellationToken);

            return rowsAffected == 1;
        }

        public async Task DeleteAsync(Transaction transaction, CancellationToken cancellationToken = default)
        {
            _context.Transactions.Remove(transaction);
            await _context.SaveChangesAsync(cancellationToken);
        }
    }
}
