using FinanceTracker.Domain.Common;
using FluentValidation;

namespace FinanceTracker.Application.Imports.GetImportJob
{
    public sealed class GetImportJobQueryValidator : AbstractValidator<GetImportJobQuery>
    {
        public GetImportJobQueryValidator()
        {
            RuleFor(q => q.ImportJobId)
                .NotEqual(default(ImportJobId))
                .WithMessage("ImportJobId is required.");
        }
    }
}
