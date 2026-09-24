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
    /// confirmed but before ProcessedAt was saved, the same row is published
    /// again next time. Consumers are idempotent for exactly this reason
    /// (docs/adr/0012-rabbitmq-topology-and-delivery-guarantees.md). The
    /// row's Id is sent as the MessageId, so a duplicate is recognizable.
    ///
    /// Assumes a single running relay. Two instances polling the same table
    /// could both pick up the same row and publish it twice -- still
    /// correct, given idempotent consumers, but wasteful. Running several
    /// Worker instances would call for row locking (SELECT ... FOR UPDATE
    /// SKIP LOCKED) here.
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
                        // Only the Api applies migrations (ADR 0008), and
                        // this process can start before it has. Expected
                        // for a few seconds after `docker compose up`.
                        _logger.LogInformation(
                            "Outbox table does not exist yet (the Api applies migrations on startup); retrying.");
                    }
                    catch (Exception ex) when (!stoppingToken.IsCancellationRequested)
                    {
                        // Anything else (database briefly unreachable, ...):
                        // log and try again on the next tick rather than
                        // letting one bad poll stop the relay for good.
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
            // A fresh scope, and so a fresh DbContext, per poll: this relay
            // lives as long as the process, a DbContext is meant to be short-
            // lived (its change tracker only grows).
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

                // Saved after every message, not once per batch, so a crash
                // halfway through a batch doesn't republish the messages
                // already confirmed. CancellationToken.None on purpose: once
                // the broker has confirmed a message, recording that is worth
                // finishing even if shutdown was requested meanwhile --
                // skipping it just guarantees a duplicate publish later.
                await dbContext.SaveChangesAsync(CancellationToken.None);
            }

            return published;
        }

        private static bool IsMissingOutboxTable(Exception ex) =>
            ex is PostgresException { SqlState: PostgresErrorCodes.UndefinedTable }
            || ex.InnerException is PostgresException { SqlState: PostgresErrorCodes.UndefinedTable };
    }
}
