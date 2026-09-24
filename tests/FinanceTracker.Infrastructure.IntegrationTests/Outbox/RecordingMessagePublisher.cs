using FinanceTracker.Infrastructure.Messaging;

namespace FinanceTracker.Infrastructure.IntegrationTests.Outbox
{
    /// <summary>
    /// Fake IMessagePublisher: records what it was asked to publish, or
    /// throws when FailPublishing is set -- so OutboxRelay can be tested
    /// against a real database without also needing a real broker (which
    /// RabbitMqMessagingTests already covers on its own).
    /// </summary>
    public sealed class RecordingMessagePublisher : IMessagePublisher
    {
        private readonly List<OutgoingMessage> _published = [];

        public bool FailPublishing { get; set; }

        public int PublishCalls { get; private set; }

        public IReadOnlyList<OutgoingMessage> Published => _published;

        public Task PublishAsync(OutgoingMessage message, CancellationToken cancellationToken = default)
        {
            PublishCalls++;

            if (FailPublishing)
                throw new InvalidOperationException("Simulated broker failure.");

            _published.Add(message);
            return Task.CompletedTask;
        }
    }
}
