using FinanceTracker.Application.Accounts;
using FinanceTracker.Application.Common;
using FinanceTracker.Domain.Common;
using FinanceTracker.Domain.Transactions;
using MediatR;

namespace FinanceTracker.Application.Transactions.AddTransaction
{
    public sealed class AddTransactionCommandHandler : IRequestHandler<AddTransactionCommand, Result<TransactionId>>
    {
        private readonly IAccountRepository _accountRepository;
        private readonly ITransactionRepository _transactionRepository;

        public AddTransactionCommandHandler(
            IAccountRepository accountRepository, ITransactionRepository transactionRepository)
        {
            _accountRepository = accountRepository;
            _transactionRepository = transactionRepository;
        }

        public async Task<Result<TransactionId>> Handle(
            AddTransactionCommand request, CancellationToken cancellationToken)
        {
            var account = await _accountRepository.GetByIdAsync(request.AccountId, cancellationToken);

            if (account is null)
            {
                return Result.Failure<TransactionId>(
                    Error.NotFound("Account.NotFound", $"No account found with id '{request.AccountId}'."));
            }

            if (account.IsClosed)
            {
                return Result.Failure<TransactionId>(
                    Error.Conflict("Account.Closed", "Cannot add a transaction to a closed account."));
            }

            var amount = Money.Create(request.Amount, account.Currency);
            var transaction = Transaction.Create(
                request.AccountId, amount, request.Type, request.Description, request.OccurredOn);

            await _transactionRepository.AddAsync(transaction, cancellationToken);

            return transaction.Id;
        }
    }
}
