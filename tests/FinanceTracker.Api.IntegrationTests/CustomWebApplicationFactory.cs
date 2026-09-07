using FinanceTracker.Infrastructure.Persistence;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Testcontainers.PostgreSql;

namespace FinanceTracker.Api.IntegrationTests
{
    /// <summary>
    /// Boots the real Api host -- the same Program.cs, the same DI wiring
    /// -- against a real, disposable Postgres container instead of
    /// whatever's in User Secrets, and applies migrations to it before any
    /// test runs. One instance is shared across every test in a class (via
    /// IClassFixture&lt;CustomWebApplicationFactory&gt;), not created fresh
    /// per test like FinanceTracker.Infrastructure.IntegrationTests does --
    /// faster, at the cost of tests within the same class sharing database
    /// state. Assert on the specific entities a test itself created, not on
    /// whole-table counts, to stay isolated from other tests in the class.
    /// </summary>
    public sealed class CustomWebApplicationFactory : WebApplicationFactory<Program>, IAsyncLifetime
    {
        private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder("postgres:16-alpine")
            .Build();

        public async Task InitializeAsync()
        {
            await _postgres.StartAsync();

            // Accessing Services triggers the host to actually build, which
            // runs ConfigureWebHost below -- by then the container is
            // already started, so its real connection string is known.
            using var scope = Services.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<FinanceTrackerDbContext>();
            await context.Database.MigrateAsync();
        }

        async Task IAsyncLifetime.DisposeAsync()
        {
            await _postgres.DisposeAsync();
            await base.DisposeAsync();
        }

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.ConfigureAppConfiguration((_, configBuilder) =>
            {
                // Overrides whatever ConnectionStrings:FinanceTracker the
                // running machine has in User Secrets/environment
                // variables -- tests always talk to this container, never
                // to a developer's local dev database.
                configBuilder.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["ConnectionStrings:FinanceTracker"] = _postgres.GetConnectionString(),
                });
            });
        }
    }
}
