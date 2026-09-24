using System.Text;
using RabbitMQ.Client;

namespace FinanceTracker.Infrastructure.Messaging
{
    /// <summary>
    /// IMessagePublisher over a single, long-lived channel with publisher
    /// confirmations enabled.
    /// </summary>
    /// <remarks>
    /// Without publisher confirmations, BasicPublishAsync returns as soon as
    /// the bytes are written to the socket -- before the broker has stored
    /// anything. A broker crash, a full disk, or a dropped connection at that
    /// moment loses the message with no error on this side. With
    /// confirmations tracking enabled, BasicPublishAsync instead waits for
    /// the broker's ack and throws a PublishException if the broker says no.
    ///
    /// mandatory: true covers the other silent-loss case: a message whose
    /// routing key matches no bound queue is normally just dropped by the
    /// exchange. With mandatory it is returned instead, which surfaces here
    /// as a PublishReturnException.
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
                    // Written to disk by the broker, not only kept in
                    // memory, so it survives a broker restart while it
                    // waits in a (durable) queue.
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
            // A channel the broker closed because of an error (publishing
            // to an exchange that doesn't exist, for example) stays closed
            // for good -- automatic recovery only covers a lost connection
            // -- so a closed one is replaced rather than reused.
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
