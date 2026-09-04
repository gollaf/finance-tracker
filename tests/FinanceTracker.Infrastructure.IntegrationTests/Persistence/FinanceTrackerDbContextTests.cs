using FinanceTracker.Infrastructure.Persistence;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Testcontainers.PostgreSql;

namespace FinanceTracker.Infrastructure.IntegrationTests.Persistence
{
    /// <summary>
    /// The walking skeleton for Infrastructure: proves the whole
    /// persistence pipeline works end to end against a real PostgreSQL
    /// instance (per ADR 0001 — no in-memory/SQLite stand-in) before any
    /// aggregate's repository is built on top of it. Each test gets its
    /// own container so tests never share database state.
    /// </summary>
    public sealed class FinanceTrackerDbContextTests : IAsyncLifetime
    {
        private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder("postgres:16-alpine")
            .Build();

        public Task InitializeAsync() => _postgres.StartAsync();

        public Task DisposeAsync() => _postgres.DisposeAsync().AsTask();

        [Fact]
        public async Task MigrateAsync_AgainstFreshContainer_AppliesInitialMigration()
        {
            var optionsBuilder = new DbContextOptionsBuilder<FinanceTrackerDbContext>()
                .UseNpgsql(_postgres.GetConnectionString());

            await using var context = new FinanceTrackerDbContext(optionsBuilder.Options);

            await context.Database.MigrateAsync();

            var appliedMigrations = await context.Database.GetAppliedMigrationsAsync();
            appliedMigrations.Should().ContainSingle();
        }
    }
}
