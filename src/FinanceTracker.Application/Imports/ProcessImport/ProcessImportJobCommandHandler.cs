using FinanceTracker.Application.Accounts;
using FinanceTracker.Application.Categorization;
using FinanceTracker.Application.Common;
using FinanceTracker.Application.Common.IntegrationEvents;
using FinanceTracker.Application.Transactions;
using FinanceTracker.Application.Transactions.IntegrationEvents;
using FinanceTracker.Domain.Categorization;
using FinanceTracker.Domain.Common;
using FinanceTracker.Domain.Imports;
using FinanceTracker.Domain.Transactions;
using MediatR;

namespace FinanceTracker.Application.Imports.ProcessImport
{
    /// <summary>
    /// See ProcessImportJobCommand.
    /// </summary>
    /// <remarks>
    /// Like the synchronous import it replaces, this handler catches a
    /// domain exception on purpose: the rows are untrusted external data, so
    /// a row failing a domain rule (Transaction.Create throwing
    /// ArgumentException) is an expected outcome, reported on the job, not a
    /// bug. Any OTHER exception -- the database becoming unreachable, say --
    /// escapes, which rolls back the whole unit of work and makes the
    /// message consumer retry from a clean state.
    /// </remarks>
    public sealed class ProcessImportJobCommandHandler
        : IRequestHandler<ProcessImportJobCommand, Result<ProcessImportJobOutcome>>
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly IImportJobRepository _importJobRepository;
        private readonly IAccountRepository _accountRepository;
        private readonly ITransactionRepository _transactionRepository;
        private readonly ICategorizationRuleRepository _categorizationRuleRepository;
        private readonly IOutbox _outbox;

        public ProcessImportJobCommandHandler(
            IUnitOfWork unitOfWork,
            IImportJobRepository importJobRepository,
            IAccountRepository accountRepository,
            ITransactionRepository transactionRepository,
            ICategorizationRuleRepository categorizationRuleRepository,
            IOutbox outbox)
        {
            _unitOfWork = unitOfWork;
            _importJobRepository = importJobRepository;
            _accountRepository = accountRepository;
            _transactionRepository = transactionRepository;
            _categorizationRuleRepository = categorizationRuleRepository;
            _outbox = outbox;
        }

        public Task<Result<ProcessImportJobOutcome>> Handle(
            ProcessImportJobCommand request, CancellationToken cancellationToken) =>
            // Everything below -- every Transaction, every outbox event, and
            // the job's own status change -- commits together or not at all.
            _unitOfWork.ExecuteAtomicallyAsync(
                token => ProcessAsync(request.ImportJobId, token), cancellationToken);

        private async Task<Result<ProcessImportJobOutcome>> ProcessAsync(
            ImportJobId importJobId, CancellationToken cancellationToken)
        {
            var importJob = await _importJobRepository.GetByIdAsync(importJobId, cancellationToken);

            if (importJob is null)
                return Result.Success(ProcessImportJobOutcome.JobNotFound);

            // The idempotency check. A job that isn't Pending was already
            // processed and committed -- by an earlier delivery of this same
            // message that crashed before it could acknowledge it.
            if (!importJob.IsPending)
                return Result.Success(ProcessImportJobOutcome.AlreadyProcessed);

            var account = await _accountRepository.GetByIdAsync(importJob.AccountId, cancellationToken);

            if (account is null || account.IsClosed)
            {
                importJob.Fail(account is null
                    ? "The account no longer exists."
                    : "The account was closed before the import could run.");
                await _importJobRepository.UpdateAsync(importJob, cancellationToken);

                return Result.Success(ProcessImportJobOutcome.Failed);
            }

            var rules = await _categorizationRuleRepository.GetAllAsync(cancellationToken);
            var rowErrors = new List<ImportJobRowError>();
            var importedCount = 0;

            foreach (var row in importJob.Rows)
            {
                try
                {
                    var amount = Money.Create(row.Amount, account.Currency);
                    var categoryId = TransactionCategorizer.Categorize(row.Description, rules);
                    var transaction = Transaction.Create(
                        account.Id, amount, row.Type, row.Description, row.OccurredOn, categoryId);

                    // Enqueue before AddAsync, per row (IOutbox's ordering rule).
                    _outbox.Enqueue(new TransactionAdded(transaction.Id.Value));
                    await _transactionRepository.AddAsync(transaction, cancellationToken);
                    importedCount++;
                }
                catch (ArgumentException ex)
                {
                    rowErrors.Add(new ImportJobRowError(row.RowNumber, ex.Message));
                }
            }

            importJob.Complete(importedCount, rowErrors);
            await _importJobRepository.UpdateAsync(importJob, cancellationToken);

            return Result.Success(ProcessImportJobOutcome.Completed);
        }
    }
}
