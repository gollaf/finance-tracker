using FinanceTracker.Domain.Accounts;
using FinanceTracker.Domain.Common;

namespace FinanceTracker.Application.Accounts
{
    /// <summary>
    /// Persistence contract for Account, implemented by Infrastructure. No
    /// implementation exists yet — handler tests satisfy this with
    /// NSubstitute mocks instead of a real database.
    /// </summary>
    public interface IAccountRepository
    {
        Task<Account?> GetByIdAsync(AccountId id, CancellationToken cancellationToken = default);

        Task AddAsync(Account account, CancellationToken cancellationToken = default);
    }
}
