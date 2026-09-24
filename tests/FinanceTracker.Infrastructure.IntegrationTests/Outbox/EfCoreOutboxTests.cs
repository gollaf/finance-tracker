using System.Text.Json;
using FinanceTracker.Domain.Accounts;
using FinanceTracker.Infrastructure.Messaging;
using FinanceTracker.Infrastructure.Outbox;
using FinanceTracker.Infrastructure.Persistence;
using FinanceTracker.Infrastructure.Persistence.Repositories;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Testcontainers.PostgreSql;

namespace FinanceTracker.Infrastructure.IntegrationTests.Outbox
{
    /// <summary>
    /// Proves the property the whole outbox pattern rests on: an enqueued
    /// event is written by the SAME SaveChangesAsync as the repository change
    /// that follows it -- and, just as importantly, by nothing else.
    /// </summary>
    public sealed class EfCoreOutboxTests : IAsyncLifetime
    {
        private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder("postgres:16-alpine")
            .Build();

        private DbContextOptions<FinanceTrackerDbContext> _options = null!;

        public async Task InitializeAsync()
        {
            await _postgres.StartAsync();

            _options = new DbContextOptionsBuilder<FinanceTrackerDbContext>()
                .UseNpgsql(_postgres.GetConnectionString())
                .Options;

            await using var context = new FinanceTrackerDbContext(_options);
            await context.Database.MigrateAsync();
        }

        public Task DisposeAsync() => _postgres.DisposeAsync().AsTask();

        [Fact]
        public async Task Enqueue_ThenRepositorySave_PersistsTheEventAndTheChangeTogether()
        {
            var account = Account.Create("Checking", AccountType.Checking, "USD");
            var integrationEvent = new TestIntegrationEvent(account.Id.Value, "account opened");

            await using (var context = new FinanceTrackerDbContext(_options))
            {
                var outbox = new EfCoreOutbox(context);
                var repository = new AccountRepository(context);

                // The ordering rule from IOutbox: enqueue first...
                outbox.Enqueue(integrationEvent);
                // ...then the repository's own SaveChangesAsync writes both.
                await repository.AddAsync(account, CancellationToken.None);
            }

            await using var verify = new FinanceTrackerDbContext(_options);

            (await verify.Accounts.CountAsync()).Should().Be(1);

            var row = await verify.OutboxMessages.SingleAsync();
            row.EventName.Should().Be(TestIntegrationEvent.EventName);
            row.Type.Should().Be(nameof(TestIntegrationEvent));
            row.ProcessedAt.Should().BeNull();
            row.Attempts.Should().Be(0);

            var roundTripped = JsonSerializer.Deserialize<TestIntegrationEvent>(row.Payload, MessageSerialization.Options);
            roundTripped.Should().Be(integrationEvent);
        }

        [Fact]
        public async Task Enqueue_WithNoSaveAfterIt_PersistsNothing()
        {
            // Why the ordering rule exists: Enqueue only stages the event on
            // the DbContext. If nothing saves that context afterwards, the
            // event silently never reaches the database.
            await using (var context = new FinanceTrackerDbContext(_options))
            {
                new EfCoreOutbox(context).Enqueue(new TestIntegrationEvent(Guid.NewGuid(), "never saved"));
            }

            await using var verify = new FinanceTrackerDbContext(_options);
            (await verify.OutboxMessages.CountAsync()).Should().Be(0);
        }
    }
}
