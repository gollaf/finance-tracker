using FinanceTracker.Domain.Common;
using FluentValidation;

namespace FinanceTracker.Application.Budgets.CreateBudget
{
    public sealed class CreateBudgetCommandValidator : AbstractValidator<CreateBudgetCommand>
    {
        public CreateBudgetCommandValidator()
        {
            RuleFor(c => c.CategoryId)
                .NotEqual(default(CategoryId))
                .WithMessage("CategoryId is required.");

            RuleFor(c => c.Year)
                .GreaterThan(0);

            RuleFor(c => c.Month)
                .InclusiveBetween(1, 12);

            RuleFor(c => c.LimitAmount)
                .GreaterThan(0m);

            RuleFor(c => c.Currency)
                .NotEmpty()
                .Matches("^[A-Z]{3}$")
                .WithMessage("Currency must be a 3-letter ISO code, e.g. USD.");
        }
    }
}
