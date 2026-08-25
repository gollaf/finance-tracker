using FinanceTracker.Domain.Common;
using FluentValidation;

namespace FinanceTracker.Application.Transactions.ImportTransactionsFromCsv
{
    /// <summary>
    /// Validates only the shape of the request — an Account is referenced
    /// and at least one row exists. Per-row content (amount, description,
    /// date) is deliberately NOT checked here: this runs through
    /// ValidationBehavior, which fails the whole command on any error, and
    /// one malformed row shouldn't sink an otherwise-good import. Per-row
    /// validation happens inside the handler instead, row by row.
    /// </summary>
    public sealed class ImportTransactionsFromCsvCommandValidator : AbstractValidator<ImportTransactionsFromCsvCommand>
    {
        public ImportTransactionsFromCsvCommandValidator()
        {
            RuleFor(c => c.AccountId)
                .NotEqual(default(AccountId))
                .WithMessage("AccountId is required.");

            RuleFor(c => c.Rows)
                .NotEmpty()
                .WithMessage("At least one row is required.");
        }
    }
}
