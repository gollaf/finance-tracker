using FinanceTracker.Domain.Common;
using FluentValidation;

namespace FinanceTracker.Application.Transactions.GetSpendingInsights
{
    public sealed class GetSpendingInsightsQueryValidator : AbstractValidator<GetSpendingInsightsQuery>
    {
        public GetSpendingInsightsQueryValidator()
        {
            RuleFor(q => q.AccountId)
                .NotEqual(default(AccountId))
                .WithMessage("AccountId is required.");

            RuleFor(q => q.Year)
                .GreaterThan(0);

            RuleFor(q => q.Month)
                .InclusiveBetween(1, 12);
        }
    }
}
