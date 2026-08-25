using FinanceTracker.Application.Common;
using FinanceTracker.Domain.Accounts;
using FinanceTracker.Domain.Common;
using MediatR;

namespace FinanceTracker.Application.Accounts.CreateAccount
{
    /// <summary>Creates a new Account. Returns the new AccountId on success.</summary>
    public sealed record CreateAccountCommand(string Name, AccountType Type, string Currency)
        : IRequest<Result<AccountId>>;
}
