using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using RabbitMQ.Client;

namespace FinanceTracker.Infrastructure.Messaging
{
    /// <summary>
    /// Connects to RabbitMQ at startup and declares the shared exchanges,
    /// then finishes. Makes a broken broker connection (wrong host, wrong
    /// credentials) visible in the logs the moment the process starts,
    /// instead of only when the first message is published or consumed.
    /// Runs as a BackgroundService so the connection retry in
    /// RabbitMqConnectionProvider never blocks the rest of the host from
    /// starting.
    /// </summary>
    public sealed class RabbitMqTopologyInitializer : BackgroundService
    {
        private readonly RabbitMqConnectionProvider _connectionProvider;
        private readonly ILogger<RabbitMqTopologyInitializer> _logger;

        public RabbitMqTopologyInitializer(
            RabbitMqConnectionProvider connectionProvider, ILogger<RabbitMqTopologyInitializer> logger)
        {
            _connectionProvider = connectionProvider;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            try
            {
                var connection = await _connectionProvider.GetConnectionAsync(stoppingToken);

                await using var channel = await connection.CreateChannelAsync(cancellationToken: stoppingToken);
                await RabbitMqTopology.DeclareExchangesAsync(channel, stoppingToken);
                await channel.CloseAsync(stoppingToken);

                _logger.LogInformation(
                    "RabbitMQ exchanges {EventsExchange} and {DeadLetterExchange} are declared.",
                    RabbitMqTopology.EventsExchange, RabbitMqTopology.DeadLetterExchange);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                // Stopped before the broker became reachable.
            }
        }
    }
}
