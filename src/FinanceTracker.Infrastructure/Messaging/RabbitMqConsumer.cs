using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace FinanceTracker.Infrastructure.Messaging
{
    /// <summary>
    /// Base class for every RabbitMQ consumer: a BackgroundService that owns
    /// one channel, declares its own queue, and turns each delivery into a
    /// call to HandleAsync. A subclass only says which queue and routing key
    /// it wants and what to do with one message; acknowledgement, retries,
    /// dead-lettering, and DI scoping all live here, once.
    /// See docs/adr/0012-rabbitmq-topology-and-delivery-guarantees.md.
    /// </summary>
    /// <remarks>
    /// Delivery is at-least-once: a message can arrive more than once (for
    /// example, if this process crashes after HandleAsync succeeded but
    /// before the ack reached the broker). Every HandleAsync implementation
    /// must therefore be idempotent -- processing the same message twice
    /// must leave the same result as processing it once.
    /// </remarks>
    public abstract class RabbitMqConsumer<TMessage> : BackgroundService
        where TMessage : class
    {
        private readonly RabbitMqConnectionProvider _connectionProvider;
        private readonly IServiceScopeFactory _scopeFactory;

        private IChannel? _channel;

        protected RabbitMqConsumer(
            RabbitMqConnectionProvider connectionProvider,
            IServiceScopeFactory scopeFactory,
            ILogger logger)
        {
            _connectionProvider = connectionProvider;
            _scopeFactory = scopeFactory;
            Logger = logger;
        }

        protected ILogger Logger { get; }

        /// <summary>The queue this consumer owns and reads from.</summary>
        protected abstract string QueueName { get; }

        /// <summary>Which events, published to RabbitMqTopology.EventsExchange, get copied into QueueName.</summary>
        protected abstract string RoutingKey { get; }

        protected virtual int DeliveryLimit => RabbitMqTopology.DefaultDeliveryLimit;

        /// <summary>
        /// Processes one message. Returning normally acknowledges it;
        /// throwing sends it back to be retried, up to DeliveryLimit
        /// attempts, after which the broker moves it to the dead-letter
        /// queue. <paramref name="services"/> is a DI scope created for this
        /// one message -- resolve repositories, IMediator, etc. from it.
        /// </summary>
        protected abstract Task HandleAsync(TMessage message, IServiceProvider services, CancellationToken cancellationToken);

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            try
            {
                var connection = await _connectionProvider.GetConnectionAsync(stoppingToken);
                var channel = await connection.CreateChannelAsync(cancellationToken: stoppingToken);
                _channel = channel;

                await RabbitMqTopology.DeclareExchangesAsync(channel, stoppingToken);
                await RabbitMqTopology.DeclareConsumerQueueAsync(
                    channel, QueueName, RoutingKey, DeliveryLimit, stoppingToken);

                // Prefetch 1: the broker hands this consumer one message at
                // a time and sends the next only after the previous one is
                // acked or rejected. Without a limit it would push the whole
                // queue into this process's memory at once -- and all of
                // those would be redelivered if the process crashed.
                await channel.BasicQosAsync(prefetchSize: 0, prefetchCount: 1, global: false, stoppingToken);

                var consumer = new AsyncEventingBasicConsumer(channel);
                consumer.ReceivedAsync += (_, delivery) => OnReceivedAsync(channel, delivery, stoppingToken);

                // autoAck: false -- the broker keeps each message until this
                // code explicitly acks it. With autoAck: true it would be
                // deleted the moment it was sent, and lost if HandleAsync
                // then failed or the process crashed mid-way.
                await channel.BasicConsumeAsync(QueueName, autoAck: false, consumer: consumer, cancellationToken: stoppingToken);

                Logger.LogInformation(
                    "Consuming from queue {Queue} (routing key {RoutingKey}).", QueueName, RoutingKey);

                // Messages arrive through the ReceivedAsync callback above,
                // not through this method -- it only needs to stay running
                // until the host stops.
                await Task.Delay(Timeout.Infinite, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                // Normal shutdown.
            }
        }

        private async Task OnReceivedAsync(IChannel channel, BasicDeliverEventArgs delivery, CancellationToken stoppingToken)
        {
            // Body is only valid for the duration of this callback (the
            // client reuses the underlying buffer), which is fine here: it
            // is deserialized immediately, before anything else is awaited.
            TMessage? message;
            try
            {
                message = JsonSerializer.Deserialize<TMessage>(delivery.Body.Span, MessageSerialization.Options);
            }
            catch (JsonException ex)
            {
                Logger.LogError(ex, "Message {MessageId} on {Queue} is not valid JSON.",
                    delivery.BasicProperties.MessageId, QueueName);
                message = null;
            }

            try
            {
                if (message is null)
                {
                    // Retrying can never fix a message that can't be read,
                    // so it goes straight to the dead-letter queue.
                    await channel.BasicRejectAsync(delivery.DeliveryTag, requeue: false);
                    return;
                }

                try
                {
                    // One DI scope per message, exactly like ASP.NET Core
                    // creates one per HTTP request. This consumer is a
                    // Singleton (every hosted service is); resolving Scoped
                    // services such as DbContext or repositories straight
                    // from the root provider would share one DbContext
                    // across every message for the life of the process.
                    await using var scope = _scopeFactory.CreateAsyncScope();
                    await HandleAsync(message, scope.ServiceProvider, stoppingToken);

                    await channel.BasicAckAsync(delivery.DeliveryTag, multiple: false);
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    // Shutting down, not a failure: hand the message back
                    // without counting it as a failed attempt (basic.nack is
                    // not counted towards x-delivery-limit; basic.reject is).
                    await channel.BasicNackAsync(delivery.DeliveryTag, multiple: false, requeue: true);
                }
                catch (Exception ex)
                {
                    Logger.LogWarning(ex,
                        "Handling message {MessageId} from {Queue} failed; returning it to the queue for retry.",
                        delivery.BasicProperties.MessageId, QueueName);

                    // basic.reject, not basic.nack: reject counts as a failed
                    // delivery towards the queue's x-delivery-limit, which is
                    // what eventually dead-letters a message that keeps
                    // failing instead of retrying it forever.
                    await channel.BasicRejectAsync(delivery.DeliveryTag, requeue: true);
                }
            }
            catch (Exception ex)
            {
                // The ack/reject itself failed -- typically because the
                // connection dropped. Nothing more can be done from here:
                // the broker still holds the message as unacknowledged and
                // will redeliver it (which is why handlers are idempotent).
                Logger.LogError(ex, "Could not acknowledge message {MessageId} on {Queue}.",
                    delivery.BasicProperties.MessageId, QueueName);
            }
        }

        public override async Task StopAsync(CancellationToken cancellationToken)
        {
            // Cancels stoppingToken and waits for ExecuteAsync to return.
            await base.StopAsync(cancellationToken);

            if (_channel is not null)
            {
                await _channel.CloseAsync(cancellationToken);
                await _channel.DisposeAsync();
            }
        }
    }
}
