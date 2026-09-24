using FinanceTracker.Application.Accounts;
using FinanceTracker.Application.Common;
using FinanceTracker.Application.Common.IntegrationEvents;
using FinanceTracker.Application.Transactions.IntegrationEvents;
using FinanceTracker.Domain.Common;
using FinanceTracker.Domain.Transactions;
using MediatR;

namespace FinanceTracker.Application.Transactions.AddTransaction
{
    public sealed class AddTransactionCommandHandler : IRequestHandler<AddTransactionCommand, Result<TransactionId>>
    {
        private readonly IAccountRepository _accountRepository;
        private readonly ITransactionRepository _transactionRepository;
        private readonly IOutbox _outbox;

        public AddTransactionCommandHandler(
            IAccountRepository accountRepository, ITransactionRepository transactionRepository, IOutbox outbox)
        {
            _accountRepository = accountRepository;
            _transactionRepository = transactionRepository;
            _outbox = outbox;
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

            // Enqueue BEFORE AddAsync: AddAsync's SaveChangesAsync is what
            // writes the outbox row too, in the same database transaction
            // (see IOutbox and docs/adr/0013-transactional-outbox.md).
            _outbox.Enqueue(new TransactionAdded(transaction.Id.Value));
            await _transactionRepository.AddAsync(transaction, cancellationToken);

            return Result.Success(transaction.Id);
        }
    }
}
