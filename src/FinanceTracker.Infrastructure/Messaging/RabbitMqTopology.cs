using RabbitMQ.Client;

namespace FinanceTracker.Infrastructure.Messaging
{
    /// <summary>
    /// Every exchange and queue this application uses, declared from code
    /// rather than clicked together in the management UI -- so a fresh broker
    /// (a new laptop, a Testcontainers instance, a Kubernetes pod) ends up
    /// with exactly the same topology with no manual steps. Every declare
    /// here is idempotent: declaring something that already exists with the
    /// same settings is a no-op, so it's safe for every process to run these
    /// on every startup. See docs/adr/0012-rabbitmq-topology-and-delivery-guarantees.md.
    /// </summary>
    public static class RabbitMqTopology
    {
        /// <summary>
        /// The single exchange every event is published to. "topic" type:
        /// queues bind with a routing-key pattern (e.g. "transaction.added",
        /// or "transaction.*" for every transaction event), so a new
        /// consumer can subscribe without the publisher changing at all.
        /// </summary>
        public const string EventsExchange = "finance-tracker.events";

        /// <summary>
        /// Where a queue sends messages it gives up on. "direct" type: each
        /// consumer queue dead-letters with its own name as the routing key,
        /// so every consumer gets its own dead-letter queue.
        /// </summary>
        public const string DeadLetterExchange = "finance-tracker.dead-letter";

        /// <summary>
        /// How many failed delivery attempts a message gets before its queue
        /// dead-letters it. Enforced by the broker itself (quorum queues'
        /// x-delivery-limit), not by this application's code.
        /// </summary>
        public const int DefaultDeliveryLimit = 5;

        public static string DeadLetterQueueName(string queueName) => $"{queueName}.dead-letter";

        public static async Task DeclareExchangesAsync(IChannel channel, CancellationToken cancellationToken = default)
        {
            // durable: true -- the exchange definition survives a broker
            // restart. autoDelete: false -- it isn't removed just because
            // no queue happens to be bound to it at the moment.
            await channel.ExchangeDeclareAsync(
                exchange: EventsExchange,
                type: ExchangeType.Topic,
                durable: true,
                autoDelete: false,
                cancellationToken: cancellationToken);

            await channel.ExchangeDeclareAsync(
                exchange: DeadLetterExchange,
                type: ExchangeType.Direct,
                durable: true,
                autoDelete: false,
                cancellationToken: cancellationToken);
        }

        /// <summary>
        /// Declares one consumer's queue, its dead-letter queue, and both
        /// bindings. Assumes DeclareExchangesAsync already ran on this
        /// channel (binding to an exchange that doesn't exist is an error).
        /// </summary>
        /// <remarks>
        /// A queue's arguments are fixed at creation: redeclaring an
        /// existing queue with *different* arguments (say, a changed
        /// deliveryLimit) doesn't update it -- the broker rejects the
        /// declare with a PRECONDITION_FAILED error and closes the channel.
        /// Changing one means deleting the queue first (management UI, or
        /// `docker compose down -v` locally).
        /// </remarks>
        public static async Task DeclareConsumerQueueAsync(
            IChannel channel,
            string queueName,
            string routingKey,
            int deliveryLimit = DefaultDeliveryLimit,
            CancellationToken cancellationToken = default)
        {
            var deadLetterQueueName = DeadLetterQueueName(queueName);

            await channel.QueueDeclareAsync(
                queue: deadLetterQueueName,
                durable: true,
                exclusive: false,
                autoDelete: false,
                arguments: new Dictionary<string, object?> { ["x-queue-type"] = "quorum" },
                cancellationToken: cancellationToken);

            await channel.QueueBindAsync(
                queue: deadLetterQueueName,
                exchange: DeadLetterExchange,
                routingKey: queueName,
                cancellationToken: cancellationToken);

            await channel.QueueDeclareAsync(
                queue: queueName,
                durable: true,
                exclusive: false,
                autoDelete: false,
                arguments: new Dictionary<string, object?>
                {
                    // Quorum queues are RabbitMQ's recommended durable queue
                    // type, and the only one that counts failed deliveries
                    // and enforces a limit on them -- the poison-message
                    // protection described on DefaultDeliveryLimit.
                    ["x-queue-type"] = "quorum",
                    ["x-delivery-limit"] = deliveryLimit,
                    ["x-dead-letter-exchange"] = DeadLetterExchange,
                    ["x-dead-letter-routing-key"] = queueName,
                },
                cancellationToken: cancellationToken);

            await channel.QueueBindAsync(
                queue: queueName,
                exchange: EventsExchange,
                routingKey: routingKey,
                cancellationToken: cancellationToken);
        }
    }
}
