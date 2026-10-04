using FinanceTracker.Application.Common.IntegrationEvents;
using FinanceTracker.Infrastructure.Messaging;
using FinanceTracker.Infrastructure.Persistence;

namespace FinanceTracker.Infrastructure.Outbox
{
    /// <summary>
    /// Stages the event as an OutboxMessage on the scope's shared DbContext
    /// without saving it. The repository save that follows writes the change
    /// and this row in one database transaction: both commit, or neither.
    /// </summary>
    public sealed class EfCoreOutbox : IOutbox
    {
        private readonly FinanceTrackerDbContext _dbContext;

        public EfCoreOutbox(FinanceTrackerDbContext dbContext)
        {
            _dbContext = dbContext;
        }

        public void Enqueue<TEvent>(TEvent integrationEvent)
            where TEvent : IIntegrationEvent
        {
            ArgumentNullException.ThrowIfNull(integrationEvent);

            _dbContext.OutboxMessages.Add(new OutboxMessage(
                id: Guid.NewGuid(),
                type: typeof(TEvent).Name,
                eventName: TEvent.EventName,
                payload: MessageSerialization.Serialize(integrationEvent),
                occurredAt: DateTimeOffset.UtcNow));
        }
    }
}
