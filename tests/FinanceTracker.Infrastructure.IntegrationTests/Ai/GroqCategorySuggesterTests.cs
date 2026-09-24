using System.Net;
using System.Text;
using System.Text.Json;
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
    /// No real network call: Groq's side is played by FakeHttpMessageHandler,
    /// same approach as GroqInsightsGeneratorTests. The real-API check is
    /// GroqCategorySuggesterSmokeTests.
    /// </summary>
    public sealed class GroqCategorySuggesterTests
    {
        private static readonly CategoryOption Groceries = new(CategoryId.New(), "Groceries");
        private static readonly CategoryOption Transport = new(CategoryId.New(), "Transport");
        private static readonly CategoryOption Dining = new(CategoryId.New(), "Dining");

        private static CategorySuggestionRequest SampleRequest() =>
            new("UBER *TRIP HELP.UBER.COM", TransactionType.Expense, new[] { Groceries, Transport, Dining });

        private static GroqCategorySuggester NewSuggester(HttpMessageHandler handler, string apiKey = "test-key")
        {
            var httpClient = new HttpClient(handler)
            {
                BaseAddress = new Uri("https://api.groq.com/"),
                Timeout = TimeSpan.FromSeconds(10),
            };

            var options = Options.Create(new GroqOptions { ApiKey = apiKey, TimeoutSeconds = 10 });
            return new GroqCategorySuggester(httpClient, options, NullLogger<GroqCategorySuggester>.Instance);
        }

        /// <summary>A fake Groq that always answers with <paramref name="answer"/> as the message content.</summary>
        private static FakeHttpMessageHandler RespondingWith(string answer) =>
            new((_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(
                    "{\"choices\":[{\"message\":{\"role\":\"assistant\",\"content\":" +
                    JsonSerializer.Serialize(answer) + "}}]}",
                    Encoding.UTF8,
                    "application/json"),
            }));

        [Theory]
        [InlineData("2")]
        [InlineData(" 2 ")]
        [InlineData("2.")]
        [InlineData("2\n")]
        public async Task SuggestAsync_WithAValidNumber_ReturnsTheIdOfThatCategory(string answer)
        {
            var result = await NewSuggester(RespondingWith(answer)).SuggestAsync(SampleRequest());

            result.IsSuccess.Should().BeTrue();
            result.Value.Should().Be(Transport.Id);
        }

        [Fact]
        public async Task SuggestAsync_WithZero_ReturnsSuccessWithNoCategory()
        {
            var result = await NewSuggester(RespondingWith("0")).SuggestAsync(SampleRequest());

            result.IsSuccess.Should().BeTrue();
            result.Value.Should().BeNull();
        }

        [Theory]
        [InlineData("Transport")]
        [InlineData("2 or 3")]
        [InlineData("I think 2")]
        [InlineData("4")]
        [InlineData("-1")]
        [InlineData("")]
        public async Task SuggestAsync_WithAnAnswerThatIsNotAnOfferedNumber_ReturnsFailure(string answer)
        {
            var result = await NewSuggester(RespondingWith(answer)).SuggestAsync(SampleRequest());

            result.IsFailure.Should().BeTrue();
            result.Error.Code.Should().Be("Groq.InvalidAnswer");
        }

        [Fact]
        public async Task SuggestAsync_SendsTheDescriptionAndANumberedCategoryList()
        {
            string? sentBody = null;
            var handler = new FakeHttpMessageHandler(async (request, cancellationToken) =>
            {
                request.RequestUri!.PathAndQuery.Should().Be("/openai/v1/chat/completions");
                request.Headers.Authorization!.Parameter.Should().Be("test-key");
                sentBody = await request.Content!.ReadAsStringAsync(cancellationToken);

                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(
                        "{\"choices\":[{\"message\":{\"role\":\"assistant\",\"content\":\"0\"}}]}",
                        Encoding.UTF8,
                        "application/json"),
                };
            });

            await NewSuggester(handler).SuggestAsync(SampleRequest());

            sentBody.Should().NotBeNull();
            using var json = JsonDocument.Parse(sentBody!);
            json.RootElement.GetProperty("temperature").GetDouble().Should().Be(0);

            var userPrompt = json.RootElement.GetProperty("messages")[1].GetProperty("content").GetString();
            userPrompt.Should().Contain("UBER *TRIP HELP.UBER.COM")
                .And.Contain("1. Groceries")
                .And.Contain("2. Transport")
                .And.Contain("3. Dining")
                .And.Contain("0. None of these");
        }

        [Fact]
        public async Task SuggestAsync_WithMissingApiKey_ReturnsFailureWithoutCallingGroq()
        {
            var called = false;
            var handler = new FakeHttpMessageHandler((_, _) =>
            {
                called = true;
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK));
            });

            var result = await NewSuggester(handler, apiKey: "").SuggestAsync(SampleRequest());

            result.IsFailure.Should().BeTrue();
            result.Error.Code.Should().Be("Groq.NotConfigured");
            called.Should().BeFalse();
        }

        [Fact]
        public async Task SuggestAsync_WithNonSuccessStatus_ReturnsFailure()
        {
            var handler = new FakeHttpMessageHandler((_, _) =>
                Task.FromResult(new HttpResponseMessage(HttpStatusCode.TooManyRequests)
                {
                    Content = new StringContent("rate limited"),
                }));

            var result = await NewSuggester(handler).SuggestAsync(SampleRequest());

            result.IsFailure.Should().BeTrue();
            result.Error.Code.Should().Be("Groq.RequestFailed");
        }

        [Fact]
        public async Task SuggestAsync_WithNoCategoriesOffered_ReturnsNoCategoryWithoutCallingGroq()
        {
            var called = false;
            var handler = new FakeHttpMessageHandler((_, _) =>
            {
                called = true;
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK));
            });

            var request = new CategorySuggestionRequest("Anything", TransactionType.Expense, Array.Empty<CategoryOption>());
            var result = await NewSuggester(handler).SuggestAsync(request);

            result.IsSuccess.Should().BeTrue();
            result.Value.Should().BeNull();
            called.Should().BeFalse();
        }
    }
}
