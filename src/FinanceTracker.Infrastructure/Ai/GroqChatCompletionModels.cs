using System.Text.Json.Serialization;

namespace FinanceTracker.Infrastructure.Ai
{
    /// <summary>
    /// Request/response JSON shapes for Groq's OpenAI-compatible chat
    /// completions endpoint. Internal: nothing outside the Groq clients
    /// should depend on Groq's wire format.
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
