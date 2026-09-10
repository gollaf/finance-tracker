using FinanceTracker.Application.Common;
using FinanceTracker.Application.Transactions;

namespace FinanceTracker.Api.IntegrationTests
{
    /// <summary>
    /// Replaces the real GroqInsightsGenerator in every Api.IntegrationTests
    /// test -- see CustomWebApplicationFactory.ConfigureWebHost. These tests
    /// exercise the full HTTP pipeline against a real Postgres
    /// (Testcontainers), but a real call to Groq would need a real API key
    /// in CI, add network flakiness to an otherwise deterministic test
    /// suite, and actually consume free-tier quota on every CI run --
    /// none of which this project wants. Always succeeding with a fixed
    /// narrative keeps GetSpendingInsights' "happy path" test
    /// deterministic; NotConfiguredInsightsGenerator's old always-fails
    /// behavior is covered separately by GetSpendingInsightsQueryHandlerTests'
    /// own fallback-narrative test in Application.UnitTests.
    /// </summary>
    public sealed class StubInsightsGenerator : IInsightsGenerator
    {
        public const string FixedNarrative = "Stubbed insight: spending looks normal this month.";

        public Task<Result<string>> GenerateAsync(
            InsightsGenerationRequest request, CancellationToken cancellationToken = default) =>
            Task.FromResult(Result.Success(FixedNarrative));
    }
}
