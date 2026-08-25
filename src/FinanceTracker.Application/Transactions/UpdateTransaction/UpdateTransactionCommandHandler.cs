using FinanceTracker.Application.Common;
using FinanceTracker.Domain.Common;
using MediatR;

namespace FinanceTracker.Application.Transactions.UpdateTransaction
{
    /// <summary>
    /// Doesn't check whether the transaction's Account is closed — only
    /// AddTransaction blocks closed accounts, since correcting existing
    /// history isn't the same as adding new activity to one.
    /// </summary>
    public sealed class UpdateTransactionCommandHandler : IRequestHandler<UpdateTransactionCommand, Result>
    {
        private readonly ITransactionRepository _transactionRepository;

        public UpdateTransactionCommandHandler(ITransactionRepository transactionRepository)
        {
            _transactionRepository = transactionRepository;
        }

        public async Task<Result> Handle(UpdateTransactionCommand request, CancellationToken cancellationToken)
        {
            var transaction = await _transactionRepository.GetByIdAsync(request.TransactionId, cancellationToken);

            if (transaction is null)
            {
                return Result.Failure(Error.NotFound(
                    "Transaction.NotFound", $"No transaction found with id '{request.TransactionId}'."));
            }

            transaction.UpdateAmount(Money.Create(request.Amount, transaction.Amount.Currency));
            transaction.UpdateDescription(request.Description);
            transaction.UpdateOccurredOn(request.OccurredOn);

            await _transactionRepository.UpdateAsync(transaction, cancellationToken);

            return Result.Success();
        }
    }
}
