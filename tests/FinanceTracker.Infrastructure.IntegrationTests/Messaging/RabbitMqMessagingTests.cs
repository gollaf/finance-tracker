using FinanceTracker.Infrastructure.Messaging;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using RabbitMQ.Client;
using RabbitMQ.Client.Exceptions;
using Testcontainers.RabbitMq;

namespace FinanceTracker.Infrastructure.IntegrationTests.Messaging
{
    /// <summary>
    /// End-to-end behavior of the RabbitMQ plumbing against a real broker
    /// (Testcontainers), the same image docker-compose.yml runs. These cover
    /// what can't be unit-tested with fakes: the broker actually routing a
    /// message, refusing an unroutable one, and dead-lettering a message that
    /// keeps failing. Each test gets its own fresh broker (IAsyncLifetime
    /// runs around every test), so no queue or message leaks between tests.
    /// </summary>
    public sealed class RabbitMqMessagingTests : IAsyncLifetime
    {
        private const string UserName = "financetracker";
        private const string Password = "financetracker";
        private const int AmqpPort = 5672;
        private static readonly TimeSpan WaitTimeout = TimeSpan.FromSeconds(30);

        private readonly RabbitMqContainer _rabbitMq = new RabbitMqBuilder("rabbitmq:4-management")
            .WithUsername(UserName)
            .WithPassword(Password)
            .Build();

        // An empty container: the test consumers resolve nothing from their
        // per-message scope, but RabbitMqConsumer still needs a scope factory.
        private readonly ServiceProvider _services = new ServiceCollection().BuildServiceProvider();

        private RabbitMqConnectionProvider _connectionProvider = null!;
        private RabbitMqPublisher _publisher = null!;

        public async Task InitializeAsync()
        {
            await _rabbitMq.StartAsync();

            var options = Options.Create(new RabbitMqOptions
            {
                Host = _rabbitMq.Hostname,
                // Docker maps the container's 5672 to a random free port on
                // this machine, so that tests running in parallel never clash.
                Port = _rabbitMq.GetMappedPublicPort(AmqpPort),
                UserName = UserName,
                Password = Password,
            });

            _connectionProvider = new RabbitMqConnectionProvider(options, NullLogger<RabbitMqConnectionProvider>.Instance);
            _publisher = new RabbitMqPublisher(_connectionProvider);
        }

        public async Task DisposeAsync()
        {
            await _publisher.DisposeAsync();
            await _connectionProvider.DisposeAsync();
            await _services.DisposeAsync();
            await _rabbitMq.DisposeAsync();
        }

        [Fact]
        public async Task PublishedMessage_IsDeliveredToTheBoundConsumer()
        {
            const string queue = "test.round-trip";
            const string routingKey = "test.message-sent";
            var received = new TaskCompletionSource<TestMessage>(TaskCreationOptions.RunContinuationsAsynchronously);

            await DeclareQueueAsync(queue, routingKey, RabbitMqTopology.DefaultDeliveryLimit);
            var consumer = CreateConsumer(queue, routingKey, RabbitMqTopology.DefaultDeliveryLimit, message =>
            {
                received.TrySetResult(message);
                return Task.CompletedTask;
            });
            await consumer.StartAsync(CancellationToken.None);

            var sent = new TestMessage(Guid.NewGuid(), "hello");
            await _publisher.PublishAsync(ToOutgoing(routingKey, MessageSerialization.Serialize(sent)));

            var actual = await received.Task.WaitAsync(WaitTimeout);
            actual.Should().Be(sent);

            await consumer.StopAsync(CancellationToken.None);
        }

        [Fact]
        public async Task Publish_WithNoQueueBoundToTheRoutingKey_Throws()
        {
            // No queue declared at all: the exchange has nowhere to route
            // this. Without mandatory: true the broker would silently drop
            // it and the publish would look successful.
            var act = () => _publisher.PublishAsync(
                ToOutgoing("test.nobody-listens", MessageSerialization.Serialize(new TestMessage(Guid.NewGuid(), "lost"))));

            await act.Should().ThrowAsync<PublishException>().Where(ex => ex.IsReturn);
        }

        [Fact]
        public async Task MessageWhoseHandlerAlwaysFails_IsRetried_ThenDeadLettered()
        {
            const string queue = "test.always-fails";
            const string routingKey = "test.always-fails";
            const int deliveryLimit = 2;
            var attempts = 0;

            await DeclareQueueAsync(queue, routingKey, deliveryLimit);
            var consumer = CreateConsumer(queue, routingKey, deliveryLimit, _ =>
            {
                Interlocked.Increment(ref attempts);
                throw new InvalidOperationException("Simulated handler failure.");
            });
            await consumer.StartAsync(CancellationToken.None);

            await _publisher.PublishAsync(
                ToOutgoing(routingKey, MessageSerialization.Serialize(new TestMessage(Guid.NewGuid(), "poison"))));

            var deadLettered = await WaitForMessageAsync(RabbitMqTopology.DeadLetterQueueName(queue));

            deadLettered.Should().NotBeNull("the broker should move the message to the dead-letter queue once the delivery limit is exceeded");
            // At least deliveryLimit attempts: exactly where the broker
            // draws the line (limit vs. limit + 1) is its own detail.
            Volatile.Read(ref attempts).Should().BeGreaterThanOrEqualTo(deliveryLimit);

            await consumer.StopAsync(CancellationToken.None);
        }

        [Fact]
        public async Task MessageThatIsNotValidJson_IsDeadLetteredWithoutReachingTheHandler()
        {
            const string queue = "test.malformed";
            const string routingKey = "test.malformed";
            var handlerCalls = 0;

            await DeclareQueueAsync(queue, routingKey, RabbitMqTopology.DefaultDeliveryLimit);
            var consumer = CreateConsumer(queue, routingKey, RabbitMqTopology.DefaultDeliveryLimit, _ =>
            {
                Interlocked.Increment(ref handlerCalls);
                return Task.CompletedTask;
            });
            await consumer.StartAsync(CancellationToken.None);

            await _publisher.PublishAsync(ToOutgoing(routingKey, "this is not json"));

            var deadLettered = await WaitForMessageAsync(RabbitMqTopology.DeadLetterQueueName(queue));

            deadLettered.Should().NotBeNull();
            Volatile.Read(ref handlerCalls).Should().Be(0);

            await consumer.StopAsync(CancellationToken.None);
        }

        private TestConsumer CreateConsumer(
            string queue, string routingKey, int deliveryLimit, Func<TestMessage, Task> handle) =>
            new(queue, routingKey, deliveryLimit, handle, _connectionProvider, _services.GetRequiredService<IServiceScopeFactory>());

        private static OutgoingMessage ToOutgoing(string routingKey, string body) =>
            new(Guid.NewGuid(), nameof(TestMessage), routingKey, body);

        /// <summary>
        /// Declares the queue before the consumer starts, so a message
        /// published right after StartAsync has somewhere to go even if the
        /// consumer's own (identical, idempotent) declare hasn't run yet.
        /// Must use the same deliveryLimit as the consumer: a queue's
        /// arguments can't differ between two declares.
        /// </summary>
        private async Task DeclareQueueAsync(string queue, string routingKey, int deliveryLimit)
        {
            var connection = await _connectionProvider.GetConnectionAsync();
            await using var channel = await connection.CreateChannelAsync();
            await RabbitMqTopology.DeclareExchangesAsync(channel);
            await RabbitMqTopology.DeclareConsumerQueueAsync(channel, queue, routingKey, deliveryLimit);
            await channel.CloseAsync();
        }

        /// <summary>Polls a queue until it has a message or WaitTimeout passes.</summary>
        private async Task<BasicGetResult?> WaitForMessageAsync(string queue)
        {
            var connection = await _connectionProvider.GetConnectionAsync();
            await using var channel = await connection.CreateChannelAsync();

            var deadline = DateTime.UtcNow + WaitTimeout;
            while (DateTime.UtcNow < deadline)
            {
                var result = await channel.BasicGetAsync(queue, autoAck: true);
                if (result is not null)
                    return result;

                await Task.Delay(TimeSpan.FromMilliseconds(250));
            }

            return null;
        }

        private sealed class TestConsumer : RabbitMqConsumer<TestMessage>
        {
            private readonly Func<TestMessage, Task> _handle;

            public TestConsumer(
                string queueName,
                string routingKey,
                int deliveryLimit,
                Func<TestMessage, Task> handle,
                RabbitMqConnectionProvider connectionProvider,
                IServiceScopeFactory scopeFactory)
                : base(connectionProvider, scopeFactory, NullLogger.Instance)
            {
                QueueName = queueName;
                RoutingKey = routingKey;
                DeliveryLimit = deliveryLimit;
                _handle = handle;
            }

            protected override string QueueName { get; }

            protected override string RoutingKey { get; }

            protected override int DeliveryLimit { get; }

            protected override Task HandleAsync(TestMessage message, IServiceProvider services, CancellationToken cancellationToken) =>
                _handle(message);
        }
    }

    public sealed record TestMessage(Guid Id, string Text);
}
