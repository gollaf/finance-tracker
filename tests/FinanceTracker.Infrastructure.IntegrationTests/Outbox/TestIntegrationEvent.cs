using FinanceTracker.Application.Common.IntegrationEvents;

namespace FinanceTracker.Infrastructure.IntegrationTests.Outbox
{
    /// <summary>A stand-in integration event for the outbox tests.</summary>
    public sealed record TestIntegrationEvent(Guid SomethingId, string Note) : IIntegrationEvent
    {
        public static string EventName => "test.something-happened";
    }
}
