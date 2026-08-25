using FinanceTracker.Domain.Common;
using FluentValidation;

namespace FinanceTracker.Application.Transactions.GetTransactions
{
    public sealed class GetTransactionsQueryValidator : AbstractValidator<GetTransactionsQuery>
    {
        public GetTransactionsQueryValidator()
        {
            RuleFor(q => q.AccountId)
                .NotEqual(default(AccountId))
                .WithMessage("AccountId is required.");

            RuleFor(q => q.To)
                .GreaterThanOrEqualTo(q => q.From!.Value)
                .When(q => q.From.HasValue && q.To.HasValue)
                .WithMessage("To date must be on or after From date.");
        }
    }
}
