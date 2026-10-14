using FinanceTracker.Domain.Common;
using FluentValidation;

namespace FinanceTracker.Application.Transactions.DeleteTransaction
{
    public sealed class DeleteTransactionCommandValidator : AbstractValidator<DeleteTransactionCommand>
    {
        public DeleteTransactionCommandValidator()
        {
            RuleFor(c => c.OwnerId)
                .NotEqual(default(UserId))
                .WithMessage("OwnerId is required.");

            RuleFor(c => c.TransactionId)
                .NotEqual(default(TransactionId))
                .WithMessage("TransactionId is required.");
        }
    }
}
