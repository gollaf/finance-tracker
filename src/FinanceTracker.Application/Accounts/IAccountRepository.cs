using FinanceTracker.Domain.Accounts;
using FinanceTracker.Domain.Common;

namespace FinanceTracker.Application.Accounts
{
    /// <summary>
    /// Persistence contract for Account, implemented by Infrastructure. Every
    /// read is scoped to one owner: another user's Account is simply not found.
    /// </summary>
    public interface IAccountRepository
    {
        Task<Account?> GetByIdAsync(UserId ownerId, AccountId id, CancellationToken cancellationToken = default);

        Task AddAsync(Account account, CancellationToken cancellationToken = default);
    }
}
