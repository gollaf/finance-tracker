using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using RabbitMQ.Client;

namespace FinanceTracker.Infrastructure.Messaging
{
    /// <summary>
    /// Connects to RabbitMQ at startup, declares the shared exchanges and
    /// every ConsumerQueue registered in DI, then finishes. Makes a broken
    /// broker connection (wrong host, wrong credentials) visible in the logs
    /// the moment the process starts, instead of only when the first message
    /// is published or consumed. Runs as a BackgroundService so the
    /// connection retry in RabbitMqConnectionProvider never blocks the rest
    /// of the host from starting.
    /// </summary>
    /// <remarks>
    /// Declaring the queues here, and not only in each consumer, is what
    /// lets a process that only publishes (the outbox relay running on its
    /// own) start before any consumer has: the queues exist from the moment
    /// any process that knows about them has started, and hold messages
    /// until a consumer comes along. The consumers still declare their own
    /// queue too -- declaring is idempotent, and it keeps each consumer
    /// self-sufficient.
    /// </remarks>
    public sealed class RabbitMqTopologyInitializer : BackgroundService
    {
        private readonly RabbitMqConnectionProvider _connectionProvider;
        private readonly IReadOnlyList<ConsumerQueue> _consumerQueues;
        private readonly ILogger<RabbitMqTopologyInitializer> _logger;

        public RabbitMqTopologyInitializer(
            RabbitMqConnectionProvider connectionProvider,
            IEnumerable<ConsumerQueue> consumerQueues,
            ILogger<RabbitMqTopologyInitializer> logger)
        {
            _connectionProvider = connectionProvider;
            _consumerQueues = consumerQueues.ToList();
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            try
            {
                var connection = await _connectionProvider.GetConnectionAsync(stoppingToken);

                await using var channel = await connection.CreateChannelAsync(cancellationToken: stoppingToken);
                await RabbitMqTopology.DeclareExchangesAsync(channel, stoppingToken);

                foreach (var queue in _consumerQueues)
                    await RabbitMqTopology.DeclareConsumerQueueAsync(channel, queue, stoppingToken);

                await channel.CloseAsync(stoppingToken);

                _logger.LogInformation(
                    "RabbitMQ exchanges {EventsExchange} and {DeadLetterExchange} are declared, with queues {Queues}.",
                    RabbitMqTopology.EventsExchange,
                    RabbitMqTopology.DeadLetterExchange,
                    string.Join(", ", _consumerQueues.Select(q => q.Name)));
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                // Stopped before the broker became reachable.
            }
        }
    }
}
