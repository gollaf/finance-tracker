using System.Text.Json.Serialization;

namespace FinanceTracker.Infrastructure.Ai
{
    /// <summary>
    /// The request/response wire shapes for Groq's OpenAI-compatible
    /// /openai/v1/chat/completions endpoint -- internal, since nothing
    /// outside GroqInsightsGenerator should ever see Groq's own JSON shape.
    /// Grouped in one file because none of these four types has any reason
    /// to exist independently of this one endpoint's contract.
    /// </summary>
    internal sealed record GroqChatCompletionRequest(
        [property: JsonPropertyName("model")] string Model,
        [property: JsonPropertyName("messages")] IReadOnlyList<GroqChatMessage> Messages,
        [property: JsonPropertyName("temperature")] double Temperature,
        [property: JsonPropertyName("max_tokens")] int MaxTokens);

    internal sealed record GroqChatMessage(
        [property: JsonPropertyName("role")] string Role,
        [property: JsonPropertyName("content")] string Content);

    internal sealed record GroqChatCompletionResponse(
        [property: JsonPropertyName("choices")] IReadOnlyList<GroqChatChoice>? Choices);

    internal sealed record GroqChatChoice(
        [property: JsonPropertyName("message")] GroqChatMessage? Message);
}
