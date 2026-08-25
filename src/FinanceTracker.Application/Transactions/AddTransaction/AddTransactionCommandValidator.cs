using FinanceTracker.Domain.Common;
using FinanceTracker.Domain.Transactions;
using FluentValidation;

namespace FinanceTracker.Application.Transactions.AddTransaction
{
    /// <summary>
    /// Mirrors Transaction's own invariants (see Transaction.cs). Whether the
    /// referenced Account actually exists, and whether it's closed, both
    /// need a repository lookup, so those checks live in the handler, not
    /// here.
    /// </summary>
    public sealed class AddTransactionCommandValidator : AbstractValidator<AddTransactionCommand>
    {
        public AddTransactionCommandValidator()
        {
            RuleFor(c => c.AccountId)
                .NotEqual(default(AccountId))
                .WithMessage("AccountId is required.");

            RuleFor(c => c.Amount)
                .GreaterThan(0m);

            RuleFor(c => c.Type)
                .IsInEnum();

            RuleFor(c => c.Description)
                .NotEmpty()
                .MaximumLength(Transaction.MaxDescriptionLength);

            RuleFor(c => c.OccurredOn)
                .LessThanOrEqualTo(_ => DateOnly.FromDateTime(DateTime.UtcNow))
                .WithMessage("Transaction date cannot be in the future.");
        }
    }
}
