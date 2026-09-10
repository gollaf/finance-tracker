using FinanceTracker.Application.Common;
using FinanceTracker.Application.Transactions.GetSpendingInsights;
using FinanceTracker.Domain.Budgets;

namespace FinanceTracker.Application.Transactions
{
    /// <summary>
    /// Port for turning already-computed spending trends into a plain-language
    /// summary. Implemented by Infrastructure (GroqInsightsGenerator) -- see
    /// docs/adr/0010-ai-insights-provider-and-integration-design.md. Never
    /// trusted with raw Transaction data or asked to compute a figure itself;
    /// every number in InsightsGenerationRequest is already correct by the
    /// time it reaches this interface.
    /// </summary>
    public interface IInsightsGenerator
    {
        /// <summary>
        /// Returns a Result.Failure (never throws) on any provider error --
        /// timeout, non-2xx response, missing configuration, malformed
        /// response -- so GetSpendingInsightsQueryHandler can fall back to a
        /// templated summary instead of failing the whole query. See ADR 0010,
        /// decision 4.
        /// </summary>
        Task<Result<string>> GenerateAsync(InsightsGenerationRequest request, CancellationToken cancellationToken = default);
    }

    /// <summary>
    /// Everything an IInsightsGenerator needs to write about one Account's
    /// spending for one month. Currency is carried separately from each
    /// CategoryTrendDto's Money values purely for convenience when Trends is
    /// empty; every Money within Trends already carries the same currency.
    /// </summary>
    public sealed record InsightsGenerationRequest(
        string Currency, BudgetPeriod Period, IReadOnlyList<CategoryTrendDto> Trends);
}
