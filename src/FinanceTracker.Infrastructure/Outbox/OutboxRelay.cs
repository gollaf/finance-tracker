using FinanceTracker.Infrastructure.Messaging;
using FinanceTracker.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Npgsql;

namespace FinanceTracker.Infrastructure.Outbox
{
    /// <summary>
    /// The second half of the transactional outbox: polls the OutboxMessages
    /// table for rows not yet published, publishes each through
    /// IMessagePublisher, and marks it processed once the broker has
    /// confirmed it. See docs/adr/0013-transactional-outbox.md.
    /// </summary>
    /// <remarks>
    /// Delivery is at-least-once: if this process stops after a publish was
    /// confirmed but before ProcessedAt was saved, the row is published again,
    /// with the same MessageId. Consumers are idempotent for this reason.
    ///
    /// Assumes a single running relay. Two would both publish the same rows
    /// (still correct, but wasteful); supporting that would need
    /// SELECT ... FOR UPDATE SKIP LOCKED here.
    /// </remarks>
    public sealed class OutboxRelay : BackgroundService
    {
        public const int BatchSize = 50;

        /// <summary>
        /// After this many failed publishes a row is no longer retried: it
        /// stays in the table, with its LastError, for someone to inspect --
        /// the outbox's equivalent of a dead-letter queue.
        /// </summary>
        public const int MaxAttempts = 10;

        private static readonly TimeSpan PollInterval = TimeSpan.FromSeconds(2);

        private readonly IServiceScopeFactory _scopeFactory;
        private readonly IMessagePublisher _publisher;
        private readonly ILogger<OutboxRelay> _logger;

        public OutboxRelay(IServiceScopeFactory scopeFactory, IMessagePublisher publisher, ILogger<OutboxRelay> logger)
        {
            _scopeFactory = scopeFactory;
            _publisher = publisher;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            using var timer = new PeriodicTimer(PollInterval);

            try
            {
                do
                {
                    try
                    {
                        await PublishPendingAsync(stoppingToken);
                    }
                    catch (Exception ex) when (IsMissingOutboxTable(ex))
                    {
                        // Migrations are applied by the Api, which may start
                        // after this process. Expected briefly on a fresh start.
                        _logger.LogInformation(
                            "Outbox table does not exist yet (migrations have not been applied); retrying.");
                    }
                    catch (Exception ex) when (!stoppingToken.IsCancellationRequested)
                    {
                        // E.g. the database is briefly unreachable: retry next tick.
                        _logger.LogError(ex, "Outbox relay poll failed; retrying in {PollInterval}.", PollInterval);
                    }
                }
                while (await timer.WaitForNextTickAsync(stoppingToken));
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                // Normal shutdown.
            }
        }

        /// <summary>
        /// One poll: publishes up to BatchSize pending rows, oldest first.
        /// Returns how many were published. Public so a test can run exactly
        /// one poll without timing the background loop.
        /// </summary>
        public async Task<int> PublishPendingAsync(CancellationToken cancellationToken = default)
        {
            // A fresh DbContext per poll; this relay lives as long as the process.
            await using var scope = _scopeFactory.CreateAsyncScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<FinanceTrackerDbContext>();

            var pending = await dbContext.OutboxMessages
                .Where(m => m.ProcessedAt == null && m.Attempts < MaxAttempts)
                .OrderBy(m => m.OccurredAt)
                .Take(BatchSize)
                .ToListAsync(cancellationToken);

            var published = 0;

            foreach (var message in pending)
            {
                try
                {
                    await _publisher.PublishAsync(
                        new OutgoingMessage(message.Id, message.Type, message.EventName, message.Payload),
                        cancellationToken);

                    message.MarkProcessed(DateTimeOffset.UtcNow);
                    published++;
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    message.RecordFailure(ex.Message);

                    if (message.Attempts >= MaxAttempts)
                    {
                        _logger.LogError(ex,
                            "Outbox message {MessageId} ({Type}) failed to publish {Attempts} times; giving up on it.",
                            message.Id, message.Type, message.Attempts);
                    }
                    else
                    {
                        _logger.LogWarning(ex,
                            "Outbox message {MessageId} ({Type}) failed to publish (attempt {Attempts} of {MaxAttempts}).",
                            message.Id, message.Type, message.Attempts, MaxAttempts);
                    }
                }

                // Saved per message so a crash mid-batch doesn't republish
                // confirmed ones. CancellationToken.None: once the broker has
                // confirmed, recording it is worth finishing during shutdown.
                await dbContext.SaveChangesAsync(CancellationToken.None);
            }

            return published;
        }

        private static bool IsMissingOutboxTable(Exception ex) =>
            ex is PostgresException { SqlState: PostgresErrorCodes.UndefinedTable }
            || ex.InnerException is PostgresException { SqlState: PostgresErrorCodes.UndefinedTable };
    }
}
