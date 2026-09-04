using FinanceTracker.Application.Accounts;
using FinanceTracker.Domain.Accounts;
using FinanceTracker.Domain.Common;
using Microsoft.EntityFrameworkCore;

namespace FinanceTracker.Infrastructure.Persistence.Repositories
{
    public sealed class AccountRepository : IAccountRepository
    {
        private readonly FinanceTrackerDbContext _context;

        public AccountRepository(FinanceTrackerDbContext context)
        {
            _context = context;
        }

        public Task<Account?> GetByIdAsync(AccountId id, CancellationToken cancellationToken = default) =>
            _context.Accounts.SingleOrDefaultAsync(a => a.Id == id, cancellationToken);

        public async Task AddAsync(Account account, CancellationToken cancellationToken = default)
        {
            await _context.Accounts.AddAsync(account, cancellationToken);
            await _context.SaveChangesAsync(cancellationToken);
        }
    }
}
