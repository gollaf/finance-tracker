namespace FinanceTracker.Infrastructure.Messaging
{
    /// <summary>
    /// Publishes a message to RabbitMqTopology.EventsExchange. Deliberately
    /// an Infrastructure interface, not an Application port: Application
    /// code never publishes to the broker directly -- it only records that
    /// an event happened, and Infrastructure decides when and how that
    /// reaches RabbitMQ. See docs/adr/0011-async-messaging-rabbitmq-raw-client.md.
    /// </summary>
    public interface IMessagePublisher
    {
        /// <summary>
        /// Completes only once the broker has confirmed it has taken
        /// responsibility for the message. Throws if it refused it, or if
        /// no queue is bound for its routing key (it would otherwise be
        /// silently discarded) -- see RabbitMqPublisher.
        /// </summary>
        Task PublishAsync(OutgoingMessage message, CancellationToken cancellationToken = default);
    }
}
