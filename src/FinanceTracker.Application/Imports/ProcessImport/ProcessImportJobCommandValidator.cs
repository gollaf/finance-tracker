using FinanceTracker.Domain.Common;
using FluentValidation;

namespace FinanceTracker.Application.Imports.ProcessImport
{
    public sealed class ProcessImportJobCommandValidator : AbstractValidator<ProcessImportJobCommand>
    {
        public ProcessImportJobCommandValidator()
        {
            RuleFor(c => c.OwnerId)
                .NotEqual(default(UserId))
                .WithMessage("OwnerId is required.");

            RuleFor(c => c.ImportJobId)
                .NotEqual(default(ImportJobId))
                .WithMessage("ImportJobId is required.");
        }
    }
}
