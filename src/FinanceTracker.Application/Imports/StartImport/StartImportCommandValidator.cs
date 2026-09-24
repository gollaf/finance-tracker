using FinanceTracker.Domain.Common;
using FinanceTracker.Domain.Imports;
using FluentValidation;

namespace FinanceTracker.Application.Imports.StartImport
{
    /// <summary>
    /// Shape only, like the synchronous import's validator was: per-row
    /// content is checked row by row during processing, where one bad row
    /// is reported instead of failing the whole import.
    /// </summary>
    public sealed class StartImportCommandValidator : AbstractValidator<StartImportCommand>
    {
        public StartImportCommandValidator()
        {
            RuleFor(c => c.AccountId)
                .NotEqual(default(AccountId))
                .WithMessage("AccountId is required.");

            RuleFor(c => c.Rows)
                .NotEmpty()
                .WithMessage("At least one row is required.");

            RuleFor(c => c.Rows.Count)
                .LessThanOrEqualTo(ImportJob.MaxRows)
                .WithMessage($"An import cannot contain more than {ImportJob.MaxRows} rows.");

            RuleFor(c => c.ParseErrors)
                .NotNull();
        }
    }
}
