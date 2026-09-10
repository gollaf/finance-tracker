using System.Net;
using System.Text;
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
    /// No real network call and no Docker -- HttpClient's message handler is
    /// substituted with FakeHttpMessageHandler, so these run in
    /// milliseconds. Filed under Infrastructure.IntegrationTests because
    /// that's this solution's only Infrastructure test project (see
    /// CompositionRootTests' own doc comment for the same situation), not
    /// because anything here actually reaches a real dependency.
    /// </summary>
    public sealed class GroqInsightsGeneratorTests
    {
        private static InsightsGenerationRequest SampleRequest() =>
            new("USD", BudgetPeriod.Create(2026, 6),
                new[]
                {
                    new CategoryTrendDto(
                        CategoryId.New(), "Groceries", Money.Create(50m, "USD"), Money.Create(30m, "USD"), 66.7m),
                });

        private static GroqInsightsGenerator NewGenerator(
            HttpMessageHandler handler, string apiKey = "test-key", TimeSpan? timeout = null)
        {
            var effectiveTimeout = timeout ?? TimeSpan.FromSeconds(10);

            var httpClient = new HttpClient(handler)
            {
                BaseAddress = new Uri("https://api.groq.com/"),
                Timeout = effectiveTimeout,
            };

            var options = Options.Create(new GroqOptions
            {
                ApiKey = apiKey,
                Model = "openai/gpt-oss-20b",
                TimeoutSeconds = (int)effectiveTimeout.TotalSeconds,
            });

            return new GroqInsightsGenerator(httpClient, options, NullLogger<GroqInsightsGenerator>.Instance);
        }

        [Fact]
        public async Task GenerateAsync_WithSuccessfulResponse_ReturnsNarrative()
        {
            var handler = new FakeHttpMessageHandler((request, _) =>
            {
                request.RequestUri!.PathAndQuery.Should().Be("/openai/v1/chat/completions");
                request.Headers.Authorization!.Scheme.Should().Be("Bearer");
                request.Headers.Authorization!.Parameter.Should().Be("test-key");

                const string responseJson =
                    """{ "choices": [ { "message": { "role": "assistant", "content": "You spent more on groceries this month." } } ] }""";

                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(responseJson, Encoding.UTF8, "application/json"),
                });
            });

            var generator = NewGenerator(handler);

            var result = await generator.GenerateAsync(SampleRequest(), CancellationToken.None);

            result.IsSuccess.Should().BeTrue();
            result.Value.Should().Be("You spent more on groceries this month.");
        }

        [Fact]
        public async Task GenerateAsync_WithMissingApiKey_ReturnsFailureWithoutCallingHttpClient()
        {
            var handler = new FakeHttpMessageHandler((_, _) =>
                throw new InvalidOperationException("HttpClient should not be called without an API key."));

            var generator = NewGenerator(handler, apiKey: string.Empty);

            var result = await generator.GenerateAsync(SampleRequest(), CancellationToken.None);

            result.IsFailure.Should().BeTrue();
            result.Error.Code.Should().Be("Groq.NotConfigured");
        }

        [Fact]
        public async Task GenerateAsync_WithNonSuccessStatusCode_ReturnsFailure()
        {
            var handler = new FakeHttpMessageHandler((_, _) =>
                Task.FromResult(new HttpResponseMessage(HttpStatusCode.TooManyRequests)
                {
                    Content = new StringContent("rate limited"),
                }));

            var generator = NewGenerator(handler);

            var result = await generator.GenerateAsync(SampleRequest(), CancellationToken.None);

            result.IsFailure.Should().BeTrue();
            result.Error.Code.Should().Be("Groq.RequestFailed");
        }

        [Fact]
        public async Task GenerateAsync_WithEmptyChoices_ReturnsFailure()
        {
            var handler = new FakeHttpMessageHandler((_, _) =>
                Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent("""{ "choices": [] }""", Encoding.UTF8, "application/json"),
                }));

            var generator = NewGenerator(handler);

            var result = await generator.GenerateAsync(SampleRequest(), CancellationToken.None);

            result.IsFailure.Should().BeTrue();
            result.Error.Code.Should().Be("Groq.EmptyResponse");
        }

        [Fact]
        public async Task GenerateAsync_WhenRequestExceedsTimeout_ReturnsFailure()
        {
            var handler = new FakeHttpMessageHandler(async (_, cancellationToken) =>
            {
                // Longer than the client's own Timeout below -- HttpClient
                // cancels the request itself once Timeout elapses, which is
                // exactly the scenario this test is proving GroqInsightsGenerator
                // turns into a Result.Failure rather than an unhandled exception.
                await Task.Delay(TimeSpan.FromSeconds(2), cancellationToken);
                return new HttpResponseMessage(HttpStatusCode.OK);
            });

            var generator = NewGenerator(handler, timeout: TimeSpan.FromMilliseconds(50));

            var result = await generator.GenerateAsync(SampleRequest(), CancellationToken.None);

            result.IsFailure.Should().BeTrue();
            result.Error.Code.Should().Be("Groq.Timeout");
        }
    }
}
