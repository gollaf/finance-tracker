using FinanceTracker.Application.Common;
using FinanceTracker.Application.Transactions;

namespace FinanceTracker.Infrastructure.Ai
{
    /// <summary>
    /// Interim IInsightsGenerator until a real provider is wired up (Phase 4
    /// Piece 2 -- GroqInsightsGenerator, which replaces this registration).
    /// Registering *something* here is required the moment
    /// GetSpendingInsightsQueryHandler exists: MediatR's assembly scan
    /// registers every IRequestHandler&lt;,&gt; it finds, and ASP.NET
    /// Core's WebApplicationFactory (used by every Api.IntegrationTests
    /// test) validates the whole DI graph can be constructed at host
    /// startup. With no IInsightsGenerator registration at all, that
    /// validation fails and every API integration test breaks -- not just
    /// ones touching GetSpendingInsights.
    ///
    /// Always returning Result.Failure is not a workaround -- it's the same
    /// "AI unavailable" path GetSpendingInsightsQueryHandler already has to
    /// handle per docs/adr/0010-ai-insights-provider-and-integration-design.md,
    /// decision 4. Until Piece 2 lands, GetSpendingInsights genuinely runs
    /// on its templated-fallback path in production, which is the correct,
    /// intended degraded behavior -- not a broken feature.
    /// </summary>
    public sealed class NotConfiguredInsightsGenerator : IInsightsGenerator
    {
        public Task<Result<string>> GenerateAsync(
            InsightsGenerationRequest request, CancellationToken cancellationToken = default) =>
            Task.FromResult(Result.Failure<string>(Error.Failure(
                "InsightsGenerator.NotConfigured", "No AI insights provider is configured yet.")));
    }
}
