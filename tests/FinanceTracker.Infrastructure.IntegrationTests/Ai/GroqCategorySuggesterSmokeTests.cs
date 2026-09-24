using FinanceTracker.Application.Transactions;
using FinanceTracker.Domain.Common;
using FinanceTracker.Domain.Transactions;
using FinanceTracker.Infrastructure.Ai;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace FinanceTracker.Infrastructure.IntegrationTests.Ai
{
    /// <summary>
    /// Calls the real Groq API -- skipped unless GROQ_API_KEY is set, exactly
    /// like GroqInsightsGeneratorSmokeTests (see that class for why). Run it
    /// after changing GroqCategorySuggester's prompt, model, or parsing:
    ///
    ///   $env:GROQ_API_KEY="gsk_..."; dotnet test --filter GroqCategorySuggesterSmokeTests
    /// </summary>
    public sealed class GroqCategorySuggesterSmokeTests
    {
        [RequiresGroqApiKey]
        public async Task SuggestAsync_AgainstRealGroqApi_PicksTheObviousCategory()
        {
            var apiKey = Environment.GetEnvironmentVariable("GROQ_API_KEY")!;

            using var httpClient = new HttpClient
            {
                BaseAddress = new Uri("https://api.groq.com/"),
                Timeout = TimeSpan.FromSeconds(15),
            };

            var options = Options.Create(new GroqOptions { ApiKey = apiKey });
            var suggester = new GroqCategorySuggester(httpClient, options, NullLogger<GroqCategorySuggester>.Instance);

            var transport = new CategoryOption(CategoryId.New(), "Transport");
            var request = new CategorySuggestionRequest(
                "UBER *TRIP HELP.UBER.COM",
                TransactionType.Expense,
                new[]
                {
                    new CategoryOption(CategoryId.New(), "Groceries"),
                    transport,
                    new CategoryOption(CategoryId.New(), "Salary"),
                });

            var result = await suggester.SuggestAsync(request, CancellationToken.None);

            // Real AI output, but an unambiguous case at temperature 0: if
            // this fails, the prompt, the answer format, or the parsing no
            // longer works against the real model.
            result.IsSuccess.Should().BeTrue(
                $"the real Groq API call should succeed, but failed with: {result.Error.Code} {result.Error.Message}");
            result.Value.Should().Be(transport.Id);
        }
    }
}
