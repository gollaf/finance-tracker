namespace FinanceTracker.Infrastructure.Messaging
{
    /// <summary>
    /// Everything needed to declare one consumer's queue: its name, the
    /// routing key that binds it to RabbitMqTopology.EventsExchange, and its
    /// delivery limit. Written down once, next to the consumer that reads the
    /// queue, and registered in DI so that RabbitMqTopologyInitializer can
    /// declare it at startup too -- in every process that talks to the
    /// broker, whether or not that process runs the consumer itself.
    /// </summary>
    /// <remarks>
    /// Why the queue can't be left to the consumer alone: the publisher uses
    /// mandatory: true, so a publish whose routing key matches no queue fails
    /// (and the outbox relay counts that as a failed attempt). A durable
    /// queue is exactly what holds messages while no consumer is running;
    /// it has to exist before the first publish, not only once some consumer
    /// has started. See docs/adr/0018-worker-roles.md.
    /// </remarks>
    public sealed record ConsumerQueue(
        string Name,
        string RoutingKey,
        int DeliveryLimit = RabbitMqTopology.DefaultDeliveryLimit);
}
