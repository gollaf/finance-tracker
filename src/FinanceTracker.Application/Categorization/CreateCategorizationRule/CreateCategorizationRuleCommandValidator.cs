using FinanceTracker.Domain.Common;
using FluentValidation;

namespace FinanceTracker.Application.Categorization.CreateCategorizationRule
{
    /// <summary>
    /// No rule on Priority — CategorizationRule.Create doesn't validate it
    /// either (see CategorizationRule.cs), and this validator mirrors Domain
    /// invariants rather than inventing extra ones.
    /// </summary>
    public sealed class CreateCategorizationRuleCommandValidator : AbstractValidator<CreateCategorizationRuleCommand>
    {
        public CreateCategorizationRuleCommandValidator()
        {
            RuleFor(c => c.Pattern)
                .NotEmpty();

            RuleFor(c => c.CategoryId)
                .NotEqual(default(CategoryId))
                .WithMessage("CategoryId is required.");
        }
    }
}
