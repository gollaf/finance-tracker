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
    /// Calls the real Groq API -- the only test that can catch a mismatch
    /// with Groq's actual wire format, which a fake handler can't reveal.
    ///
    /// Skipped unless GROQ_API_KEY is set (it isn't in CI). Run it after
    /// changing GroqInsightsGenerator or its prompt:
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

            // Not asserting on the wording: real AI output isn't deterministic.
            result.IsSuccess.Should().BeTrue(
                $"the real Groq API call should succeed, but failed with: {result.Error.Code} {result.Error.Message}");
            result.Value.Should().NotBeNullOrWhiteSpace();
        }
    }
}
