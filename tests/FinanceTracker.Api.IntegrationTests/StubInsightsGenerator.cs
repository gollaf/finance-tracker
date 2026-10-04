using FinanceTracker.Application.Common;
using FinanceTracker.Application.Transactions;

namespace FinanceTracker.Api.IntegrationTests
{
    /// <summary>
    /// Replaces the real GroqInsightsGenerator in the Api integration tests:
    /// no API key in CI, no network flakiness, no quota used. The fallback
    /// path is covered by GetSpendingInsightsQueryHandlerTests.
    /// </summary>
    public sealed class StubInsightsGenerator : IInsightsGenerator
    {
        public const string FixedNarrative = "Stubbed insight: spending looks normal this month.";

        public Task<Result<string>> GenerateAsync(
            InsightsGenerationRequest request, CancellationToken cancellationToken = default) =>
            Task.FromResult(Result.Success(FixedNarrative));
    }
}
