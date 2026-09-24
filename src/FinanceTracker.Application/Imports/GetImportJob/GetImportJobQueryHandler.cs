using FinanceTracker.Application.Common;
using MediatR;

namespace FinanceTracker.Application.Imports.GetImportJob
{
    public sealed class GetImportJobQueryHandler : IRequestHandler<GetImportJobQuery, Result<ImportJobDto>>
    {
        private readonly IImportJobRepository _importJobRepository;

        public GetImportJobQueryHandler(IImportJobRepository importJobRepository)
        {
            _importJobRepository = importJobRepository;
        }

        public async Task<Result<ImportJobDto>> Handle(GetImportJobQuery request, CancellationToken cancellationToken)
        {
            var importJob = await _importJobRepository.GetByIdAsync(request.ImportJobId, cancellationToken);

            if (importJob is null)
            {
                return Result.Failure<ImportJobDto>(Error.NotFound(
                    "ImportJob.NotFound", $"No import job found with id '{request.ImportJobId}'."));
            }

            return Result.Success(new ImportJobDto(
                importJob.Id,
                importJob.AccountId,
                importJob.Status,
                importJob.Rows.Count,
                importJob.ImportedCount,
                importJob.Errors,
                importJob.FailureReason,
                importJob.CreatedAt,
                importJob.CompletedAt));
        }
    }
}
