using FinanceTracker.Domain.Common;
using FluentValidation;

namespace FinanceTracker.Application.Accounts.GetAccountBalance
{
    public sealed class GetAccountBalanceQueryValidator : AbstractValidator<GetAccountBalanceQuery>
    {
        public GetAccountBalanceQueryValidator()
        {
            RuleFor(q => q.AccountId)
                .NotEqual(default(AccountId))
                .WithMessage("AccountId is required.");
        }
    }
}
