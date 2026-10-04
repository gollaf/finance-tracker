using FinanceTracker.Application.Categories;
using FinanceTracker.Application.Common;
using MediatR;

namespace FinanceTracker.Application.Transactions.SuggestCategory
{
    /// <summary>
    /// See SuggestCategoryForTransactionCommand. Every check here exists so
    /// this can run any number of times for the same Transaction (messages
    /// are delivered at least once) and never override a Category someone
    /// else already chose.
    /// </summary>
    public sealed class SuggestCategoryForTransactionCommandHandler
        : IRequestHandler<SuggestCategoryForTransactionCommand, Result<CategorySuggestionOutcome>>
    {
        private readonly ITransactionRepository _transactionRepository;
        private readonly ICategoryRepository _categoryRepository;
        private readonly ICategorySuggester _categorySuggester;

        public SuggestCategoryForTransactionCommandHandler(
            ITransactionRepository transactionRepository,
            ICategoryRepository categoryRepository,
            ICategorySuggester categorySuggester)
        {
            _transactionRepository = transactionRepository;
            _categoryRepository = categoryRepository;
            _categorySuggester = categorySuggester;
        }

        public async Task<Result<CategorySuggestionOutcome>> Handle(
            SuggestCategoryForTransactionCommand request, CancellationToken cancellationToken)
        {
            var transaction = await _transactionRepository.GetByIdAsync(request.TransactionId, cancellationToken);

            if (transaction is null)
                return Result.Success(CategorySuggestionOutcome.TransactionNotFound);

            // The idempotency check: a duplicate delivery of the same
            // message, or a Transaction the user categorized while this
            // message waited in the queue, both stop here -- before spending
            // an AI call.
            if (transaction.CategoryId is not null)
                return Result.Success(CategorySuggestionOutcome.AlreadyCategorized);

            var categories = await _categoryRepository.GetAllAsync(cancellationToken);

            if (categories.Count == 0)
                return Result.Success(CategorySuggestionOutcome.NoCategoriesDefined);

            var options = categories.Select(c => new CategoryOption(c.Id, c.Name)).ToList();

            var suggestion = await _categorySuggester.SuggestAsync(
                new CategorySuggestionRequest(transaction.Description, transaction.Type, options),
                cancellationToken);

            if (suggestion.IsFailure)
                return Result.Success(CategorySuggestionOutcome.SuggestionUnavailable);

            if (suggestion.Value is not { } suggestedCategoryId)
                return Result.Success(CategorySuggestionOutcome.NoSuitableCategory);

            // Defense in depth: never write an id that wasn't offered, even
            // though the port promises not to return one.
            if (!options.Any(o => o.Id == suggestedCategoryId))
                return Result.Success(CategorySuggestionOutcome.SuggestionUnavailable);

            // A conditional update, not Recategorize + UpdateAsync: the user
            // may have categorized this Transaction by hand during the AI
            // call, and a plain update would overwrite that.
            var updated = await _transactionRepository.TrySetCategoryIfUncategorizedAsync(
                transaction.Id, suggestedCategoryId, cancellationToken);

            return Result.Success(updated
                ? CategorySuggestionOutcome.Categorized
                : CategorySuggestionOutcome.AlreadyCategorized);
        }
    }
}
