using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using RabbitMQ.Client;

namespace FinanceTracker.Infrastructure.Messaging
{
    /// <summary>
    /// Connects to RabbitMQ at startup, declares the exchanges and every
    /// ConsumerQueue registered in DI, then finishes. A broken broker
    /// connection shows up in the logs right at startup. A BackgroundService,
    /// so the connection retry never blocks the host from starting.
    /// </summary>
    /// <remarks>
    /// Declaring the queues here, not only in each consumer, means they exist
    /// even when the outbox relay runs on its own before any consumer starts.
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
