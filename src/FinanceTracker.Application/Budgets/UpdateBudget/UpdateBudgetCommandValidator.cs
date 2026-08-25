using FinanceTracker.Domain.Common;
using FluentValidation;

namespace FinanceTracker.Application.Budgets.UpdateBudget
{
    public sealed class UpdateBudgetCommandValidator : AbstractValidator<UpdateBudgetCommand>
    {
        public UpdateBudgetCommandValidator()
        {
            RuleFor(c => c.BudgetId)
                .NotEqual(default(BudgetId))
                .WithMessage("BudgetId is required.");

            RuleFor(c => c.LimitAmount)
                .GreaterThan(0m);
        }
    }
}
