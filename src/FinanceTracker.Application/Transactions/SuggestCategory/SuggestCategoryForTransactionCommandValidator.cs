using FinanceTracker.Domain.Common;
using FluentValidation;

namespace FinanceTracker.Application.Transactions.SuggestCategory
{
    public sealed class SuggestCategoryForTransactionCommandValidator : AbstractValidator<SuggestCategoryForTransactionCommand>
    {
        public SuggestCategoryForTransactionCommandValidator()
        {
            RuleFor(c => c.TransactionId)
                .NotEqual(default(TransactionId))
                .WithMessage("TransactionId is required.");
        }
    }
}
