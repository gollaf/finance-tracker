using FinanceTracker.Application.Accounts;
using FinanceTracker.Application.Common;
using MediatR;

namespace FinanceTracker.Application.Transactions.GetTransactions
{
    /// <summary>
    /// Filtering happens in memory over GetByAccountIdAsync's full result —
    /// fine at this scale with no database yet. Pushing the date range down
    /// into a real query (a SQL WHERE clause) is an Infrastructure concern
    /// for whenever EF Core lands.
    /// </summary>
    public sealed class GetTransactionsQueryHandler
        : IRequestHandler<GetTransactionsQuery, Result<IReadOnlyList<TransactionSummaryDto>>>
    {
        private readonly IAccountRepository _accountRepository;
        private readonly ITransactionRepository _transactionRepository;

        public GetTransactionsQueryHandler(
            IAccountRepository accountRepository, ITransactionRepository transactionRepository)
        {
            _accountRepository = accountRepository;
            _transactionRepository = transactionRepository;
        }

        public async Task<Result<IReadOnlyList<TransactionSummaryDto>>> Handle(
            GetTransactionsQuery request, CancellationToken cancellationToken)
        {
            var account = await _accountRepository.GetByIdAsync(request.AccountId, cancellationToken);

            if (account is null)
            {
                return Result.Failure<IReadOnlyList<TransactionSummaryDto>>(Error.NotFound(
                    "Account.NotFound", $"No account found with id '{request.AccountId}'."));
            }

            var transactions = await _transactionRepository.GetByAccountIdAsync(request.AccountId, cancellationToken);

            IReadOnlyList<TransactionSummaryDto> results = transactions
                .Where(t => request.From is null || t.OccurredOn >= request.From)
                .Where(t => request.To is null || t.OccurredOn <= request.To)
                .OrderByDescending(t => t.OccurredOn)
                .Select(t => new TransactionSummaryDto(
                    t.Id, t.AccountId, t.CategoryId, t.Amount, t.Type, t.Description, t.OccurredOn))
                .ToList();

            return Result.Success(results);
        }
    }
}
