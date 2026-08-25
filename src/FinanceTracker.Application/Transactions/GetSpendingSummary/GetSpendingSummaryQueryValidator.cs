using FinanceTracker.Domain.Common;
using FluentValidation;

namespace FinanceTracker.Application.Transactions.GetSpendingSummary
{
    public sealed class GetSpendingSummaryQueryValidator : AbstractValidator<GetSpendingSummaryQuery>
    {
        public GetSpendingSummaryQueryValidator()
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
