using FinanceTracker.Infrastructure.Outbox;
using FinanceTracker.Infrastructure.Persistence;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Testcontainers.PostgreSql;

namespace FinanceTracker.Infrastructure.IntegrationTests.Outbox
{
    /// <summary>
    /// OutboxRelay against a real database and a fake publisher: each test
    /// calls PublishPendingAsync directly -- exactly one poll -- rather than
    /// starting the background loop and waiting on a timer.
    /// </summary>
    public sealed class OutboxRelayTests : IAsyncLifetime
    {
        private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder("postgres:16-alpine")
            .Build();

        private readonly RecordingMessagePublisher _publisher = new();

        private ServiceProvider _services = null!;
        private OutboxRelay _relay = null!;

        public async Task InitializeAsync()
        {
            await _postgres.StartAsync();

            // The relay creates its own DI scope (and so its own DbContext)
            // per poll, the same way it does in the Worker -- so it needs a
            // real container to create them from, not a DbContext instance.
            _services = new ServiceCollection()
                .AddDbContext<FinanceTrackerDbContext>(options => options.UseNpgsql(_postgres.GetConnectionString()))
                .BuildServiceProvider();

            await using (var scope = _services.CreateAsyncScope())
            {
                await scope.ServiceProvider.GetRequiredService<FinanceTrackerDbContext>().Database.MigrateAsync();
            }

            _relay = new OutboxRelay(
                _services.GetRequiredService<IServiceScopeFactory>(), _publisher, NullLogger<OutboxRelay>.Instance);
        }

        public async Task DisposeAsync()
        {
            await _services.DisposeAsync();
            await _postgres.DisposeAsync();
        }

        [Fact]
        public async Task PublishPendingAsync_PublishesPendingMessagesOldestFirst_AndMarksThemProcessed()
        {
            var first = await EnqueueAndSaveAsync(new TestIntegrationEvent(Guid.NewGuid(), "first"));
            var second = await EnqueueAndSaveAsync(new TestIntegrationEvent(Guid.NewGuid(), "second"));

            var published = await _relay.PublishPendingAsync();

            published.Should().Be(2);
            _publisher.Published.Select(m => m.MessageId).Should().Equal(first.Id, second.Id);
            _publisher.Published.Should().OnlyContain(m => m.RoutingKey == TestIntegrationEvent.EventName);

            var rows = await LoadAllAsync();
            rows.Should().OnlyContain(m => m.ProcessedAt != null);
        }

        [Fact]
        public async Task PublishPendingAsync_DoesNotRepublishAlreadyProcessedMessages()
        {
            await EnqueueAndSaveAsync(new TestIntegrationEvent(Guid.NewGuid(), "once"));

            await _relay.PublishPendingAsync();
            var secondPoll = await _relay.PublishPendingAsync();

            secondPoll.Should().Be(0);
            _publisher.PublishCalls.Should().Be(1);
        }

        [Fact]
        public async Task PublishPendingAsync_WhenPublishFails_RecordsTheAttemptAndKeepsTheMessagePending()
        {
            await EnqueueAndSaveAsync(new TestIntegrationEvent(Guid.NewGuid(), "broker down"));
            _publisher.FailPublishing = true;

            var published = await _relay.PublishPendingAsync();

            published.Should().Be(0);
            var row = (await LoadAllAsync()).Single();
            row.ProcessedAt.Should().BeNull();
            row.Attempts.Should().Be(1);
            row.LastError.Should().Contain("Simulated broker failure");
        }

        [Fact]
        public async Task PublishPendingAsync_StopsRetryingAMessageAfterMaxAttempts()
        {
            await EnqueueAndSaveAsync(new TestIntegrationEvent(Guid.NewGuid(), "always fails"));
            _publisher.FailPublishing = true;

            for (var poll = 0; poll < OutboxRelay.MaxAttempts; poll++)
                await _relay.PublishPendingAsync();

            _publisher.PublishCalls.Should().Be(OutboxRelay.MaxAttempts);

            // Even with the broker healthy again, a message that already
            // used up its attempts stays parked for manual inspection.
            _publisher.FailPublishing = false;
            await _relay.PublishPendingAsync();

            _publisher.PublishCalls.Should().Be(OutboxRelay.MaxAttempts);
            var row = (await LoadAllAsync()).Single();
            row.Attempts.Should().Be(OutboxRelay.MaxAttempts);
            row.ProcessedAt.Should().BeNull();
        }

        private async Task<OutboxMessage> EnqueueAndSaveAsync(TestIntegrationEvent integrationEvent)
        {
            await using var scope = _services.CreateAsyncScope();
            var context = scope.ServiceProvider.GetRequiredService<FinanceTrackerDbContext>();

            new EfCoreOutbox(context).Enqueue(integrationEvent);
            await context.SaveChangesAsync();

            // Distinct OccurredAt values, so "oldest first" is unambiguous.
            await Task.Delay(TimeSpan.FromMilliseconds(20));

            return await context.OutboxMessages.OrderByDescending(m => m.OccurredAt).FirstAsync();
        }

        private async Task<List<OutboxMessage>> LoadAllAsync()
        {
            await using var scope = _services.CreateAsyncScope();
            return await scope.ServiceProvider.GetRequiredService<FinanceTrackerDbContext>()
                .OutboxMessages.AsNoTracking().ToListAsync();
        }
    }
}
