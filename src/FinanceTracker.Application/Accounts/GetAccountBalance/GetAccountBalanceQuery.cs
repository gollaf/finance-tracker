using FinanceTracker.Application.Common;
using FinanceTracker.Domain.Common;
using MediatR;

namespace FinanceTracker.Application.Accounts.GetAccountBalance
{
    /// <summary>
    /// Computes an Account's balance by summing its Transactions — Account
    /// itself stores no Balance, per
    /// docs/adr/0002-transaction-separate-aggregate-no-stored-balance.md.
    /// </summary>
    public sealed record GetAccountBalanceQuery(AccountId AccountId) : IRequest<Result<Money>>;
}
