using FinanceTracker.Domain.Common;
using FluentValidation;

namespace FinanceTracker.Application.Categories.CreateCategory
{
    /// <summary>
    /// No MaximumLength rule on Name — Category.ValidateName only rejects
    /// empty/whitespace, with no length cap to mirror.
    /// </summary>
    public sealed class CreateCategoryCommandValidator : AbstractValidator<CreateCategoryCommand>
    {
        public CreateCategoryCommandValidator()
        {
            RuleFor(c => c.Name)
                .NotEmpty();

            RuleFor(c => c.ParentCategoryId)
                .Must(parentCategoryId => parentCategoryId is null || parentCategoryId.Value != default(CategoryId))
                .WithMessage("ParentCategoryId, if provided, must not be empty.");
        }
    }
}
