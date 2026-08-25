using FinanceTracker.Application.Common;
using FinanceTracker.Application.Transactions;
using FinanceTracker.Domain.Common;
using MediatR;

namespace FinanceTracker.Application.Accounts.GetAccountBalance
{
    /// <summary>
    /// Doesn't check whether the Account is closed — a closed account's
    /// historical balance is still meaningful to look up, unlike writes
    /// such as AddTransaction which block closed accounts outright.
    /// </summary>
    public sealed class GetAccountBalanceQueryHandler : IRequestHandler<GetAccountBalanceQuery, Result<Money>>
    {
        private readonly IAccountRepository _accountRepository;
        private readonly ITransactionRepository _transactionRepository;

        public GetAccountBalanceQueryHandler(
            IAccountRepository accountRepository, ITransactionRepository transactionRepository)
        {
            _accountRepository = accountRepository;
            _transactionRepository = transactionRepository;
        }

        public async Task<Result<Money>> Handle(GetAccountBalanceQuery request, CancellationToken cancellationToken)
        {
            var account = await _accountRepository.GetByIdAsync(request.AccountId, cancellationToken);

            if (account is null)
            {
                return Result.Failure<Money>(Error.NotFound(
                    "Account.NotFound", $"No account found with id '{request.AccountId}'."));
            }

            var transactions = await _transactionRepository.GetByAccountIdAsync(request.AccountId, cancellationToken);

            var balance = transactions.Aggregate(
                Money.Zero(account.Currency), (total, transaction) => total + transaction.SignedAmount);

            return Result.Success(balance);
        }
    }
}
