using FinanceTracker.Domain.Accounts;
using FluentValidation;

namespace FinanceTracker.Application.Accounts.CreateAccount
{
    /// <summary>
    /// Mirrors Account's own invariants (see Account.cs) so bad input is
    /// rejected here, with a clear message, before it ever reaches the
    /// Domain layer — not instead of the Domain's own checks.
    /// </summary>
    public sealed class CreateAccountCommandValidator : AbstractValidator<CreateAccountCommand>
    {
        public CreateAccountCommandValidator()
        {
            RuleFor(c => c.Name)
                .NotEmpty()
                .MaximumLength(Account.MaxNameLength);

            RuleFor(c => c.Type)
                .IsInEnum();

            RuleFor(c => c.Currency)
                .NotEmpty()
                .Matches("^[A-Z]{3}$")
                .WithMessage("Currency must be a 3-letter uppercase ISO 4217 code (e.g. \"USD\").");
        }
    }
}
