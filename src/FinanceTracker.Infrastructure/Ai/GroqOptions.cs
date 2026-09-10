namespace FinanceTracker.Infrastructure.Ai
{
    /// <summary>
    /// Bound from the "Groq" configuration section. ApiKey is supplied via
    /// User Secrets in development (dotnet user-secrets set "Groq:ApiKey"
    /// "..."), the same mechanism already used for the Postgres connection
    /// string outside Docker -- never committed to appsettings.json. See
    /// docs/adr/0010-ai-insights-provider-and-integration-design.md.
    /// </summary>
    public sealed class GroqOptions
    {
        public const string SectionName = "Groq";

        /// <summary>
        /// Empty by default. GroqInsightsGenerator treats a missing key as
        /// a non-fatal Result.Failure, not an exception -- see ADR 0010,
        /// decision 4 -- so the app runs fine without one, just without
        /// AI-generated narratives.
        /// </summary>
        public string ApiKey { get; set; } = string.Empty;

        /// <summary>
        /// openai/gpt-oss-20b: Groq's fastest general-purpose model and, as
        /// of this decision, comfortably within the free tier's rate
        /// limits -- more than enough for one narrative per
        /// GetSpendingInsights call.
        /// </summary>
        public string Model { get; set; } = "openai/gpt-oss-20b";

        /// <summary>
        /// Applied to HttpClient.Timeout by AddInfrastructure's DI wiring.
        /// Short and explicit because this call sits inline in a
        /// request/response cycle in this phase -- see ADR 0010,
        /// decision 4.
        /// </summary>
        public int TimeoutSeconds { get; set; } = 10;
    }
}
