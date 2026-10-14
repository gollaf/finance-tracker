namespace FinanceTracker.Api.RateLimiting
{
    /// <summary>Request limits per client address, bound from the "RateLimiting" section.</summary>
    public sealed class RateLimitingSettings
    {
        public const string SectionName = "RateLimiting";

        /// <summary>Requests one address may make per Window, to any endpoint.</summary>
        public int PermitLimit { get; set; } = 100;

        public TimeSpan Window { get; set; } = TimeSpan.FromMinutes(1);

        /// <summary>
        /// Requests one address may make to /api/auth per AuthWindow. Stricter:
        /// registering and guessing passwords are what bots automate.
        /// </summary>
        public int AuthPermitLimit { get; set; } = 10;

        public TimeSpan AuthWindow { get; set; } = TimeSpan.FromMinutes(5);
    }
}
