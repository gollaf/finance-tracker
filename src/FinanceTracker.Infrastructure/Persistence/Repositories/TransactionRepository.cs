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

        // Unfiltered by Account on purpose, matching the interface's own
        // comment: a Budget targets a Category, not an Account, so this has
        // to find every Transaction against that Category across every
        // Account.
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

        // ExecuteUpdateAsync sends one UPDATE ... WHERE straight to the
        // database -- no loading, no change tracking, no SaveChangesAsync:
        //   UPDATE "Transactions" SET "CategoryId" = @categoryId
        //   WHERE "Id" = @transactionId AND "CategoryId" IS NULL
        // The "still uncategorized?" check and the write happen in the same
        // statement, so nothing can change the row in between. Zero rows
        // affected means it was already categorized (or no longer exists).
        //
        // Because it bypasses the change tracker, a Transaction instance
        // this DbContext already has loaded is NOT updated in memory -- load
        // it again (from a new DbContext, or with AsNoTracking) to see the
        // new value.
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
