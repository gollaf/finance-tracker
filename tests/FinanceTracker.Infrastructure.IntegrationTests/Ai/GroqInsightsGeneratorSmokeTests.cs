using FinanceTracker.Application.Transactions;
using FinanceTracker.Application.Transactions.GetSpendingInsights;
using FinanceTracker.Domain.Budgets;
using FinanceTracker.Domain.Common;
using FinanceTracker.Infrastructure.Ai;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace FinanceTracker.Infrastructure.IntegrationTests.Ai
{
    /// <summary>
    /// Calls the real Groq API over the real network -- deliberately not
    /// covered by GroqInsightsGeneratorTests, which fakes HttpClient's
    /// transport (see that class's own doc comment for why). This is the
    /// one place in the whole test suite that can catch a real
    /// integration bug against Groq's actual API -- a wire-format mismatch,
    /// a changed response shape, an auth header Groq actually rejects --
    /// none of which a fake handler could ever reveal, because a fake
    /// handler only ever proves this code handles the responses *I*
    /// assumed Groq would send.
    ///
    /// Silent in CI (no GROQ_API_KEY there, by design -- see
    /// RequiresGroqApiKeyAttribute and
    /// docs/adr/0010-ai-insights-provider-and-integration-design.md).
    /// Costs a small amount of real free-tier quota and takes a real
    /// network round-trip, so it's opt-in rather than part of the normal
    /// `dotnet test` run everyone does on every change -- run it
    /// deliberately after touching GroqInsightsGenerator or its prompt, or
    /// periodically to confirm Groq hasn't changed anything underneath it:
    ///
    ///   GROQ_API_KEY=gsk_... dotnet test --filter GroqInsightsGeneratorSmokeTests
    /// </summary>
    public sealed class GroqInsightsGeneratorSmokeTests
    {
        [RequiresGroqApiKey]
        public async Task GenerateAsync_AgainstRealGroqApi_ReturnsNonEmptyNarrative()
        {
            var apiKey = Environment.GetEnvironmentVariable("GROQ_API_KEY")!;

            using var httpClient = new HttpClient
            {
                BaseAddress = new Uri("https://api.groq.com/"),
                Timeout = TimeSpan.FromSeconds(15),
            };

            var options = Options.Create(new GroqOptions { ApiKey = apiKey });
            var generator = new GroqInsightsGenerator(httpClient, options, NullLogger<GroqInsightsGenerator>.Instance);

            var request = new InsightsGenerationRequest(
                "USD",
                BudgetPeriod.Create(2026, 6),
                new[]
                {
                    new CategoryTrendDto(
                        CategoryId.New(), "Groceries", Money.Create(450m, "USD"), Money.Create(380m, "USD"), 18.4m),
                    new CategoryTrendDto(
                        CategoryId.New(), "Dining Out", Money.Create(120m, "USD"), Money.Create(95m, "USD"), 26.3m),
                });

            var result = await generator.GenerateAsync(request, CancellationToken.None);

            // Deliberately not asserting on the exact wording -- it's real,
            // non-deterministic AI output. The point of this test is that
            // Groq accepted the request and returned something usable, not
            // what specifically it said.
            result.IsSuccess.Should().BeTrue(
                $"the real Groq API call should succeed, but failed with: {result.Error.Code} {result.Error.Message}");
            result.Value.Should().NotBeNullOrWhiteSpace();
        }
    }
}
