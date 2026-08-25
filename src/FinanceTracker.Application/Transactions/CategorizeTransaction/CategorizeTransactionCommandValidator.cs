using FinanceTracker.Domain.Common;
using FluentValidation;

namespace FinanceTracker.Application.Transactions.CategorizeTransaction
{
    public sealed class CategorizeTransactionCommandValidator : AbstractValidator<CategorizeTransactionCommand>
    {
        public CategorizeTransactionCommandValidator()
        {
            RuleFor(c => c.TransactionId)
                .NotEqual(default(TransactionId))
                .WithMessage("TransactionId is required.");

            RuleFor(c => c.CategoryId)
                .Must(categoryId => categoryId is null || categoryId.Value != default(CategoryId))
                .WithMessage("CategoryId, if provided, must not be empty.");
        }
    }
}
