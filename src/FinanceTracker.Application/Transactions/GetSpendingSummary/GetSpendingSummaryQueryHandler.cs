using FinanceTracker.Application.Accounts;
using FinanceTracker.Application.Common;
using FinanceTracker.Domain.Budgets;
using FinanceTracker.Domain.Common;
using FinanceTracker.Domain.Transactions;
using MediatR;

namespace FinanceTracker.Application.Transactions.GetSpendingSummary
{
    /// <summary>
    /// Scoped to one Account, same as GetAccountBalance and GetTransactions —
    /// a true across-all-accounts summary would need IAccountRepository to
    /// list every account, which it doesn't support yet.
    /// </summary>
    public sealed class GetSpendingSummaryQueryHandler
        : IRequestHandler<GetSpendingSummaryQuery, Result<IReadOnlyList<CategorySpendingDto>>>
    {
        private readonly IAccountRepository _accountRepository;
        private readonly ITransactionRepository _transactionRepository;

        public GetSpendingSummaryQueryHandler(
            IAccountRepository accountRepository, ITransactionRepository transactionRepository)
        {
            _accountRepository = accountRepository;
            _transactionRepository = transactionRepository;
        }

        public async Task<Result<IReadOnlyList<CategorySpendingDto>>> Handle(
            GetSpendingSummaryQuery request, CancellationToken cancellationToken)
        {
            var account = await _accountRepository.GetByIdAsync(request.AccountId, cancellationToken);

            if (account is null)
            {
                return Result.Failure<IReadOnlyList<CategorySpendingDto>>(Error.NotFound(
                    "Account.NotFound", $"No account found with id '{request.AccountId}'."));
            }

            var period = BudgetPeriod.Create(request.Year, request.Month);

            var transactions = await _transactionRepository.GetByAccountIdAsync(request.AccountId, cancellationToken);

            IReadOnlyList<CategorySpendingDto> summary = transactions
                .Where(t => t.Type == TransactionType.Expense)
                .Where(t => period.Contains(t.OccurredOn))
                .GroupBy(t => t.CategoryId)
                .Select(g => new CategorySpendingDto(
                    g.Key, g.Aggregate(Money.Zero(account.Currency), (total, t) => total + t.Amount)))
                .OrderByDescending(s => s.Total.Amount)
                .ToList();

            return Result.Success(summary);
        }
    }
}
