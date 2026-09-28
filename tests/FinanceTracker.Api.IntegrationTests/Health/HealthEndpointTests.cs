using System.Net;
using FluentAssertions;

namespace FinanceTracker.Api.IntegrationTests.Health
{
    public sealed class HealthEndpointTests : IClassFixture<UnmigratedWebApplicationFactory>
    {
        private readonly UnmigratedWebApplicationFactory _factory;

        public HealthEndpointTests(UnmigratedWebApplicationFactory factory)
        {
            _factory = factory;
        }

        // One test, not three: its steps depend on each other's database
        // state (unmigrated, then migrated), and xUnit doesn't guarantee the
        // order separate tests in a class run in.
        [Fact]
        public async Task ReadyEndpoint_ReportsUnavailableUntilMigrationsAreApplied_WhileLiveStaysHealthy()
        {
            var client = _factory.CreateClient();

            var liveBeforeMigrations = await client.GetAsync("/health/live");
            var readyBeforeMigrations = await client.GetAsync("/health/ready");

            liveBeforeMigrations.StatusCode.Should().Be(HttpStatusCode.OK);
            readyBeforeMigrations.StatusCode.Should().Be(HttpStatusCode.ServiceUnavailable);

            await _factory.ApplyMigrationsAsync();

            var readyAfterMigrations = await client.GetAsync("/health/ready");

            readyAfterMigrations.StatusCode.Should().Be(HttpStatusCode.OK);
        }
    }
}
