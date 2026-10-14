using FinanceTracker.Application.Common.IntegrationEvents;

namespace FinanceTracker.Application.Imports.IntegrationEvents
{
    /// <summary>
    /// An ImportJob was created and is waiting to be processed. Carries only
    /// the job's id and owner -- the rows themselves are stored on the job. See
    /// docs/adr/0015-integration-event-contracts.md.
    /// </summary>
    public sealed record ImportRequested(Guid ImportJobId, Guid OwnerId) : IIntegrationEvent
    {
        public static string EventName => "import.requested";
    }
}
