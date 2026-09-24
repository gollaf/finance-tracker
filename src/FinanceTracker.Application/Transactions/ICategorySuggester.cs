using FinanceTracker.Application.Common;
using FinanceTracker.Domain.Common;
using FinanceTracker.Domain.Transactions;

namespace FinanceTracker.Application.Transactions
{
    /// <summary>
    /// Port for asking an AI which existing Category a Transaction belongs
    /// to. Implemented by Infrastructure (GroqCategorySuggester) -- see
    /// docs/adr/0014-ai-transaction-categorization.md. Same shape as
    /// IInsightsGenerator: never throws for a provider problem, returns a
    /// Result.Failure instead.
    /// </summary>
    public interface ICategorySuggester
    {
        /// <summary>
        /// Success with a CategoryId: one of request.Categories' ids, never
        /// any other value. Success with null: the AI answered, and said
        /// none of the categories fits. Failure: no usable answer (provider
        /// unreachable, not configured, timed out, or replied with something
        /// that isn't a valid choice).
        /// </summary>
        Task<Result<CategoryId?>> SuggestAsync(CategorySuggestionRequest request, CancellationToken cancellationToken = default);
    }

    /// <summary>
    /// What the AI is shown: the Transaction's description and direction,
    /// and the complete list of Categories it may choose from. Deliberately
    /// not the amount, account, or date -- the least data that does the
    /// job is all that leaves this application for a third-party API.
    /// </summary>
    public sealed record CategorySuggestionRequest(
        string Description, TransactionType Type, IReadOnlyList<CategoryOption> Categories);

    public sealed record CategoryOption(CategoryId Id, string Name);
}
