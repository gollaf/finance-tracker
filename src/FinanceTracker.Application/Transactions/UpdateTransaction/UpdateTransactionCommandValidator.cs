using FinanceTracker.Domain.Common;
using FinanceTracker.Domain.Transactions;
using FluentValidation;

namespace FinanceTracker.Application.Transactions.UpdateTransaction
{
    /// <summary>Mirrors Transaction's own invariants (see Transaction.cs).</summary>
    public sealed class UpdateTransactionCommandValidator : AbstractValidator<UpdateTransactionCommand>
    {
        public UpdateTransactionCommandValidator()
        {
            RuleFor(c => c.TransactionId)
                .NotEqual(default(TransactionId))
                .WithMessage("TransactionId is required.");

            RuleFor(c => c.Amount)
                .GreaterThan(0m);

            RuleFor(c => c.Description)
                .NotEmpty()
                .MaximumLength(Transaction.MaxDescriptionLength);

            RuleFor(c => c.OccurredOn)
                .LessThanOrEqualTo(_ => DateOnly.FromDateTime(DateTime.UtcNow))
                .WithMessage("Transaction date cannot be in the future.");
        }
    }
}
