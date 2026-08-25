using FinanceTracker.Domain.Common;
using FluentValidation;

namespace FinanceTracker.Application.Budgets.GetBudgetStatus
{
    public sealed class GetBudgetStatusQueryValidator : AbstractValidator<GetBudgetStatusQuery>
    {
        public GetBudgetStatusQueryValidator()
        {
            RuleFor(q => q.BudgetId)
                .NotEqual(default(BudgetId))
                .WithMessage("BudgetId is required.");
        }
    }
}
