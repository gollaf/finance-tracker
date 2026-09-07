using FinanceTracker.Domain.Accounts;

namespace FinanceTracker.Api.Accounts
{
    public sealed record CreateAccountRequest(string Name, AccountType Type, string Currency);
}
