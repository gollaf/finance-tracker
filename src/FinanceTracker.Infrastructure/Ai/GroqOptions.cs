namespace FinanceTracker.Infrastructure.Ai
{
    /// <summary>
    /// Bound from the "Groq" configuration section. ApiKey comes from User
    /// Secrets or an environment variable, never from appsettings.json.
    /// </summary>
    public sealed class GroqOptions
    {
        public const string SectionName = "Groq";

        /// <summary>
        /// Empty by default. The app runs without a key; the AI features
        /// then fall back instead of failing.
        /// </summary>
        public string ApiKey { get; set; } = string.Empty;

        /// <summary>A fast general-purpose model that fits within Groq's free-tier rate limits.</summary>
        public string Model { get; set; } = "openai/gpt-oss-20b";

        /// <summary>
        /// Applied to HttpClient.Timeout. Short, because the insights call
        /// runs inside an HTTP request.
        /// </summary>
        public int TimeoutSeconds { get; set; } = 10;
    }
}
