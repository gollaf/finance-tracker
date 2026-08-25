using FinanceTracker.Application.Categories;
using FinanceTracker.Application.Common;
using MediatR;

namespace FinanceTracker.Application.Transactions.CategorizeTransaction
{
    /// <summary>
    /// Confirms the target Category exists before assigning it — but only
    /// when CategoryId is provided; clearing a category (null) needs no
    /// such check.
    /// </summary>
    public sealed class CategorizeTransactionCommandHandler : IRequestHandler<CategorizeTransactionCommand, Result>
    {
        private readonly ITransactionRepository _transactionRepository;
        private readonly ICategoryRepository _categoryRepository;

        public CategorizeTransactionCommandHandler(
            ITransactionRepository transactionRepository, ICategoryRepository categoryRepository)
        {
            _transactionRepository = transactionRepository;
            _categoryRepository = categoryRepository;
        }

        public async Task<Result> Handle(CategorizeTransactionCommand request, CancellationToken cancellationToken)
        {
            var transaction = await _transactionRepository.GetByIdAsync(request.TransactionId, cancellationToken);

            if (transaction is null)
            {
                return Result.Failure(Error.NotFound(
                    "Transaction.NotFound", $"No transaction found with id '{request.TransactionId}'."));
            }

            if (request.CategoryId is { } categoryId)
            {
                var category = await _categoryRepository.GetByIdAsync(categoryId, cancellationToken);

                if (category is null)
                {
                    return Result.Failure(Error.NotFound(
                        "Category.NotFound", $"No category found with id '{categoryId}'."));
                }
            }

            transaction.Recategorize(request.CategoryId);

            await _transactionRepository.UpdateAsync(transaction, cancellationToken);

            return Result.Success();
        }
    }
}
