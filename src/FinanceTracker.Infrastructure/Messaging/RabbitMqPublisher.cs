using System.Text;
using RabbitMQ.Client;

namespace FinanceTracker.Infrastructure.Messaging
{
    /// <summary>
    /// IMessagePublisher over a single, long-lived channel with publisher
    /// confirmations enabled.
    /// </summary>
    /// <remarks>
    /// Publisher confirmations make BasicPublishAsync wait for the broker's
    /// ack and throw if it refuses the message. mandatory: true makes a
    /// message that matches no queue throw instead of being dropped silently.
    /// </remarks>
    public sealed class RabbitMqPublisher : IMessagePublisher, IAsyncDisposable
    {
        private readonly RabbitMqConnectionProvider _connectionProvider;

        // One channel shared by every publish, so publishes are serialized
        // through this lock rather than interleaving on the same channel.
        private readonly SemaphoreSlim _publishLock = new(1, 1);

        private IChannel? _channel;

        public RabbitMqPublisher(RabbitMqConnectionProvider connectionProvider)
        {
            _connectionProvider = connectionProvider;
        }

        public async Task PublishAsync(OutgoingMessage message, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(message);

            await _publishLock.WaitAsync(cancellationToken);
            try
            {
                var channel = await GetOpenChannelAsync(cancellationToken);

                var properties = new BasicProperties
                {
                    ContentType = "application/json",
                    // Survives a broker restart while waiting in a queue.
                    Persistent = true,
                    MessageId = message.MessageId.ToString(),
                    Type = message.Type,
                };

                await channel.BasicPublishAsync(
                    exchange: RabbitMqTopology.EventsExchange,
                    routingKey: message.RoutingKey,
                    mandatory: true,
                    basicProperties: properties,
                    body: Encoding.UTF8.GetBytes(message.Body),
                    cancellationToken: cancellationToken);
            }
            finally
            {
                _publishLock.Release();
            }
        }

        private async Task<IChannel> GetOpenChannelAsync(CancellationToken cancellationToken)
        {
            // A channel the broker closed after an error stays closed
            // (recovery only covers lost connections), so replace it.
            if (_channel is { IsOpen: true })
                return _channel;

            if (_channel is not null)
                await _channel.DisposeAsync();

            var connection = await _connectionProvider.GetConnectionAsync(cancellationToken);

            _channel = await connection.CreateChannelAsync(
                new CreateChannelOptions(
                    publisherConfirmationsEnabled: true,
                    publisherConfirmationTrackingEnabled: true),
                cancellationToken);

            // Publishing to an exchange that doesn't exist makes the broker
            // close the channel, so make sure it exists first. Idempotent.
            await RabbitMqTopology.DeclareExchangesAsync(_channel, cancellationToken);

            return _channel;
        }

        public async ValueTask DisposeAsync()
        {
            if (_channel is not null)
            {
                await _channel.CloseAsync();
                await _channel.DisposeAsync();
            }

            _publishLock.Dispose();
        }
    }
}
