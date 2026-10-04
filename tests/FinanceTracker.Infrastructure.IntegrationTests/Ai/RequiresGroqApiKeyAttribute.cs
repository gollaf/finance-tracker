namespace FinanceTracker.Infrastructure.IntegrationTests.Ai
{
    /// <summary>
    /// A [Fact] that runs only when GROQ_API_KEY is set, and is reported as
    /// "Skipped" otherwise -- not as a silent pass, which an early return
    /// inside the test would give.
    /// </summary>
    public sealed class RequiresGroqApiKeyAttribute : FactAttribute
    {
        public RequiresGroqApiKeyAttribute()
        {
            if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("GROQ_API_KEY")))
            {
                Skip = "Set the GROQ_API_KEY environment variable to run this test against the real Groq API.";
            }
        }
    }
}
