using FinanceTracker.Application.Accounts;
using FinanceTracker.Application.Categorization;
using FinanceTracker.Application.Common;
using FinanceTracker.Domain.Categorization;
using FinanceTracker.Domain.Common;
using FinanceTracker.Domain.Transactions;
using MediatR;

namespace FinanceTracker.Application.Transactions.ImportTransactionsFromCsv
{
    /// <summary>
    /// The one handler in this codebase that catches a domain exception on
    /// purpose. Every other handler trusts FluentValidation to have already
    /// rejected bad input, so a Domain throw there would mean a
    /// validator/domain drift bug. Here the rows are untrusted external data
    /// the validator deliberately doesn't inspect row-by-row — a bad row is
    /// an expected outcome of importing a real bank statement, not a bug, so
    /// it's caught and reported instead of failing the whole batch.
    /// </summary>
    public sealed class ImportTransactionsFromCsvCommandHandler
        : IRequestHandler<ImportTransactionsFromCsvCommand, Result<ImportTransactionsResult>>
    {
        private readonly IAccountRepository _accountRepository;
        private readonly ITransactionRepository _transactionRepository;
        private readonly ICategorizationRuleRepository _categorizationRuleRepository;

        public ImportTransactionsFromCsvCommandHandler(
            IAccountRepository accountRepository,
            ITransactionRepository transactionRepository,
            ICategorizationRuleRepository categorizationRuleRepository)
        {
            _accountRepository = accountRepository;
            _transactionRepository = transactionRepository;
            _categorizationRuleRepository = categorizationRuleRepository;
        }

        public async Task<Result<ImportTransactionsResult>> Handle(
            ImportTransactionsFromCsvCommand request, CancellationToken cancellationToken)
        {
            var account = await _accountRepository.GetByIdAsync(request.AccountId, cancellationToken);

            if (account is null)
            {
                return Result.Failure<ImportTransactionsResult>(Error.NotFound(
                    "Account.NotFound", $"No account found with id '{request.AccountId}'."));
            }

            if (account.IsClosed)
            {
                return Result.Failure<ImportTransactionsResult>(Error.Conflict(
                    "Account.Closed", "Cannot import transactions into a closed account."));
            }

            var rules = await _categorizationRuleRepository.GetAllAsync(cancellationToken);

            var importedIds = new List<TransactionId>();
            var errors = new List<ImportRowError>();

            for (var rowIndex = 0; rowIndex < request.Rows.Count; rowIndex++)
            {
                var row = request.Rows[rowIndex];

                try
                {
                    var amount = Money.Create(row.Amount, account.Currency);
                    var categoryId = TransactionCategorizer.Categorize(row.Description, rules);
                    var transaction = Transaction.Create(
                        account.Id, amount, row.Type, row.Description, row.OccurredOn, categoryId);

                    await _transactionRepository.AddAsync(transaction, cancellationToken);
                    importedIds.Add(transaction.Id);
                }
                catch (ArgumentException ex)
                {
                    errors.Add(new ImportRowError(rowIndex, ex.Message));
                }
            }

            return new ImportTransactionsResult(importedIds, errors);
        }
    }
}
