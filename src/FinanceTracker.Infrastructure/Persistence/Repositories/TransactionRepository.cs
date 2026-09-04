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

        public async Task DeleteAsync(Transaction transaction, CancellationToken cancellationToken = default)
        {
            _context.Transactions.Remove(transaction);
            await _context.SaveChangesAsync(cancellationToken);
        }
    }
}
