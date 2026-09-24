using FinanceTracker.Application.Common.IntegrationEvents;

namespace FinanceTracker.Application.Imports.IntegrationEvents
{
    /// <summary>
    /// An ImportJob was created and is waiting to be processed. Carries only
    /// the job's id -- the rows themselves are stored on the job
    /// (docs/adr/0015-integration-event-contracts.md,
    /// docs/adr/0016-asynchronous-csv-import.md).
    /// </summary>
    public sealed record ImportRequested(Guid ImportJobId) : IIntegrationEvent
    {
        public static string EventName => "import.requested";
    }
}
