using FinanceTracker.Infrastructure.Persistence;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Testcontainers.PostgreSql;

namespace FinanceTracker.Infrastructure.IntegrationTests.Persistence
{
    /// <summary>
    /// The persistence pipeline against a real PostgreSQL instance (no
    /// in-memory stand-in). Each test gets its own container.
    /// </summary>
    public sealed class FinanceTrackerDbContextTests : IAsyncLifetime
    {
        private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder("postgres:16-alpine")
            .Build();

        public Task InitializeAsync() => _postgres.StartAsync();

        public Task DisposeAsync() => _postgres.DisposeAsync().AsTask();

        [Fact]
        public async Task MigrateAsync_AgainstFreshContainer_AppliesPendingMigrations()
        {
            var optionsBuilder = new DbContextOptionsBuilder<FinanceTrackerDbContext>()
                .UseNpgsql(_postgres.GetConnectionString());

            await using var context = new FinanceTrackerDbContext(optionsBuilder.Options);

            await context.Database.MigrateAsync();

            var appliedMigrations = await context.Database.GetAppliedMigrationsAsync();

            // Not a specific count, so new migrations don't break this test.
            appliedMigrations.Should().NotBeEmpty();
        }
    }
}
