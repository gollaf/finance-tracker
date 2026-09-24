using FinanceTracker.Application.Common;
using FinanceTracker.Domain.Common;
using MediatR;

namespace FinanceTracker.Application.Transactions.SuggestCategory
{
    /// <summary>
    /// Asks the AI to categorize one uncategorized Transaction, choosing only
    /// among existing Categories, and saves its choice if it made one. Sent
    /// by the Worker in response to a "transaction.added" integration event,
    /// not exposed over HTTP. See docs/adr/0014-ai-transaction-categorization.md.
    /// </summary>
    /// <remarks>
    /// Always returns Result.Success with a CategorySuggestionOutcome for
    /// every expected situation -- including the AI being unavailable --
    /// because none of them is something retrying the same message would
    /// fix. Only an unexpected exception (the database being unreachable,
    /// say) escapes, and that is what makes the message consumer retry.
    /// </remarks>
    public sealed record SuggestCategoryForTransactionCommand(TransactionId TransactionId)
        : IRequest<Result<CategorySuggestionOutcome>>;
}
