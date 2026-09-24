using FinanceTracker.Application.Common.IntegrationEvents;
using FinanceTracker.Infrastructure.Messaging;
using FinanceTracker.Infrastructure.Persistence;

namespace FinanceTracker.Infrastructure.Outbox
{
    /// <summary>
    /// IOutbox implementation: stages the event as an OutboxMessage on the
    /// same (Scoped, one-per-request) FinanceTrackerDbContext every
    /// repository in this request/message scope also uses -- but does not
    /// save it. The repository call that follows saves the change the event
    /// describes, and EF Core's SaveChangesAsync writes everything tracked by
    /// the context -- that change AND this outbox row -- in a single
    /// database transaction. Both are committed, or neither is.
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
