using FinanceTracker.Application.Accounts;
using FinanceTracker.Application.Common;
using FinanceTracker.Application.Common.IntegrationEvents;
using FinanceTracker.Application.Imports.IntegrationEvents;
using FinanceTracker.Domain.Common;
using FinanceTracker.Domain.Imports;
using MediatR;

namespace FinanceTracker.Application.Imports.StartImport
{
    public sealed class StartImportCommandHandler : IRequestHandler<StartImportCommand, Result<ImportJobId>>
    {
        private readonly IAccountRepository _accountRepository;
        private readonly IImportJobRepository _importJobRepository;
        private readonly IOutbox _outbox;

        public StartImportCommandHandler(
            IAccountRepository accountRepository, IImportJobRepository importJobRepository, IOutbox outbox)
        {
            _accountRepository = accountRepository;
            _importJobRepository = importJobRepository;
            _outbox = outbox;
        }

        public async Task<Result<ImportJobId>> Handle(StartImportCommand request, CancellationToken cancellationToken)
        {
            // Checked here too, not only during processing: an unknown or
            // closed Account is something the uploader can be told right
            // away, in the response, instead of only after polling the job.
            var account = await _accountRepository.GetByIdAsync(request.AccountId, cancellationToken);

            if (account is null)
            {
                return Result.Failure<ImportJobId>(Error.NotFound(
                    "Account.NotFound", $"No account found with id '{request.AccountId}'."));
            }

            if (account.IsClosed)
            {
                return Result.Failure<ImportJobId>(Error.Conflict(
                    "Account.Closed", "Cannot import transactions into a closed account."));
            }

            var importJob = ImportJob.Create(request.AccountId, request.Rows, request.ParseErrors);

            // Enqueue BEFORE AddAsync, as always: AddAsync's SaveChangesAsync
            // writes the job and its ImportRequested event together (IOutbox).
            _outbox.Enqueue(new ImportRequested(importJob.Id.Value));
            await _importJobRepository.AddAsync(importJob, cancellationToken);

            return Result.Success(importJob.Id);
        }
    }
}
