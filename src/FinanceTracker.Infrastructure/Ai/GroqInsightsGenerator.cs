using System.Globalization;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using FinanceTracker.Application.Common;
using FinanceTracker.Application.Transactions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace FinanceTracker.Infrastructure.Ai
{
    /// <summary>
    /// IInsightsGenerator implementation calling Groq's OpenAI-compatible
    /// chat completions endpoint. See
    /// docs/adr/0010-ai-insights-provider-and-integration-design.md for why
    /// Groq, why this never throws, and why every number in the prompt
    /// comes from InsightsGenerationRequest, never invented here.
    /// </summary>
    public sealed class GroqInsightsGenerator : IInsightsGenerator
    {
        private const string ChatCompletionsPath = "openai/v1/chat/completions";

        private const string SystemPrompt =
            "You are a personal finance assistant. You will be given a list of spending " +
            "categories with this month's total, the average of the prior three months, and " +
            "the percent change between them, all already calculated and correct. Write a " +
            "short (2-3 sentence), friendly, plain-language summary of the most notable " +
            "changes. Use only the numbers you are given - never calculate, estimate, or " +
            "invent a figure of your own. If a category has no percent change given, do not " +
            "state or imply one for it.";

        private readonly HttpClient _httpClient;
        private readonly GroqOptions _options;
        private readonly ILogger<GroqInsightsGenerator> _logger;

        public GroqInsightsGenerator(HttpClient httpClient, IOptions<GroqOptions> options, ILogger<GroqInsightsGenerator> logger)
        {
            _httpClient = httpClient;
            _options = options.Value;
            _logger = logger;
        }

        public async Task<Result<string>> GenerateAsync(
            InsightsGenerationRequest request, CancellationToken cancellationToken = default)
        {
            // No API key configured -- a completely normal state until the
            // developer sets one via User Secrets (see ADR 0010). Fail
            // fast, before ever touching HttpClient, so this looks
            // identical in behavior to Groq being unreachable: a
            // Result.Failure the caller falls back from.
            if (string.IsNullOrWhiteSpace(_options.ApiKey))
            {
                _logger.LogInformation(
                    "Groq API key is not configured; GetSpendingInsights will fall back to a templated narrative.");
                return Result.Failure<string>(Error.Failure(
                    "Groq.NotConfigured", "Groq API key is not configured."));
            }

            var payload = new GroqChatCompletionRequest(
                _options.Model,
                new[]
                {
                    new GroqChatMessage("system", SystemPrompt),
                    new GroqChatMessage("user", BuildUserPrompt(request)),
                },
                Temperature: 0.4,
                MaxTokens: 200);

            try
            {
                using var httpRequest = new HttpRequestMessage(HttpMethod.Post, ChatCompletionsPath)
                {
                    Content = JsonContent.Create(payload),
                };
                httpRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _options.ApiKey);

                using var response = await _httpClient.SendAsync(httpRequest, cancellationToken);

                if (!response.IsSuccessStatusCode)
                {
                    var body = await response.Content.ReadAsStringAsync(cancellationToken);
                    _logger.LogWarning("Groq responded with {StatusCode}: {Body}", (int)response.StatusCode, body);
                    return Result.Failure<string>(Error.Failure(
                        "Groq.RequestFailed", $"Groq responded with status {(int)response.StatusCode}."));
                }

                var completion = await response.Content.ReadFromJsonAsync<GroqChatCompletionResponse>(cancellationToken);
                var narrative = completion?.Choices?.FirstOrDefault()?.Message?.Content?.Trim();

                if (string.IsNullOrWhiteSpace(narrative))
                {
                    _logger.LogWarning("Groq returned a response with no narrative content.");
                    return Result.Failure<string>(Error.Failure(
                        "Groq.EmptyResponse", "Groq returned an empty response."));
                }

                return Result.Success(narrative);
            }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                // HttpClient.Timeout expiring surfaces as an
                // OperationCanceledException even though the caller's own
                // CancellationToken was never signaled -- the `when` clause
                // tells the two apart. A genuinely cancelled request (the
                // caller's own token fired) is allowed to propagate instead
                // of being swallowed into a Result.
                _logger.LogWarning("Groq request timed out after {TimeoutSeconds}s.", _options.TimeoutSeconds);
                return Result.Failure<string>(Error.Failure("Groq.Timeout", "The request to Groq timed out."));
            }
            catch (HttpRequestException ex)
            {
                _logger.LogWarning(ex, "Failed to reach Groq.");
                return Result.Failure<string>(Error.Failure("Groq.RequestFailed", "Failed to reach Groq."));
            }
        }

        private static string BuildUserPrompt(InsightsGenerationRequest request)
        {
            var period = new DateOnly(request.Period.Year, request.Period.Month, 1)
                .ToString("MMMM yyyy", CultureInfo.InvariantCulture);

            var lines = request.Trends.Select(t =>
            {
                var changeText = t.PercentChange is { } change
                    ? $"{(change >= 0 ? "+" : string.Empty)}{change.ToString(CultureInfo.InvariantCulture)}%"
                    : "no prior spending to compare against";

                return $"- {t.CategoryName}: {t.CurrentMonthTotal.Amount.ToString(CultureInfo.InvariantCulture)} " +
                    $"{request.Currency} this month, {t.PriorAverageTotal.Amount.ToString(CultureInfo.InvariantCulture)} " +
                    $"{request.Currency} average over the prior 3 months ({changeText})";
            });

            return $"Month: {period}\nCurrency: {request.Currency}\n" +
                $"Category spending vs. 3-month average:\n{string.Join("\n", lines)}";
        }
    }
}
