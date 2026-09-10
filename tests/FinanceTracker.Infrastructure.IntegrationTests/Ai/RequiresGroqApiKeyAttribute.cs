namespace FinanceTracker.Infrastructure.IntegrationTests.Ai
{
    /// <summary>
    /// A [Fact] that only actually runs when a real GROQ_API_KEY environment
    /// variable is present -- otherwise xUnit reports it "Skipped", not
    /// failed. This is the standard xUnit v2 idiom for a runtime-conditional
    /// skip: FactAttribute.Skip, when non-null, is read at test discovery
    /// time, so setting it from the constructor based on an environment
    /// variable works, while a plain "if no key, return early" inside the
    /// test body would not -- that would just report a silent pass instead
    /// of the more honest "this never actually ran."
    ///
    /// Keeps GroqInsightsGeneratorSmokeTests silent in CI (no key there, by
    /// design -- see docs/adr/0010-ai-insights-provider-and-integration-design.md)
    /// while still letting a developer with a real key run it on demand:
    /// `GROQ_API_KEY=gsk_... dotnet test --filter GroqInsightsGeneratorSmokeTests`.
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
