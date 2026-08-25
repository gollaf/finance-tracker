using FinanceTracker.Application.Common;
using MediatR;

namespace FinanceTracker.Application.Transactions.DeleteTransaction
{
    /// <summary>
    /// Doesn't check whether the transaction's Account is closed, for the
    /// same reason UpdateTransaction doesn't — removing a mis-entered
    /// transaction from a closed account's history isn't "new activity."
    /// </summary>
    public sealed class DeleteTransactionCommandHandler : IRequestHandler<DeleteTransactionCommand, Result>
    {
        private readonly ITransactionRepository _transactionRepository;

        public DeleteTransactionCommandHandler(ITransactionRepository transactionRepository)
        {
            _transactionRepository = transactionRepository;
        }

        public async Task<Result> Handle(DeleteTransactionCommand request, CancellationToken cancellationToken)
        {
            var transaction = await _transactionRepository.GetByIdAsync(request.TransactionId, cancellationToken);

            if (transaction is null)
            {
                return Result.Failure(Error.NotFound(
                    "Transaction.NotFound", $"No transaction found with id '{request.TransactionId}'."));
            }

            await _transactionRepository.DeleteAsync(transaction, cancellationToken);

            return Result.Success();
        }
    }
}
