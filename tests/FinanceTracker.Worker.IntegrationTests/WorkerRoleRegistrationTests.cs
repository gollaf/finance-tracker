using FinanceTracker.Infrastructure.Messaging;
using FinanceTracker.Infrastructure.Outbox;
using FinanceTracker.Worker.Consumers;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace FinanceTracker.Worker.IntegrationTests
{
    /// <summary>
    /// Which hosted services AddWorker registers for each combination of
    /// Worker:RunOutboxRelay and Worker:RunConsumers. Inspects the
    /// registrations only -- nothing is built or started -- so, unlike the
    /// pipeline tests, these need no containers and run in milliseconds.
    /// </summary>
    public sealed class WorkerRoleRegistrationTests
    {
        private static readonly Type[] Consumers =
        [
            typeof(TransactionAddedCategorizationConsumer),
            typeof(ImportRequestedConsumer),
        ];

        [Fact]
        public void ByDefault_RegistersTheRelayAndEveryConsumer()
        {
            var hostedServices = HostedServicesFor(new Dictionary<string, string?>());

            hostedServices.Should().Contain(typeof(OutboxRelay));
            hostedServices.Should().Contain(Consumers);
        }

        [Fact]
        public void RelayOnly_RegistersTheRelayButNoConsumer()
        {
            var hostedServices = HostedServicesFor(new Dictionary<string, string?>
            {
                [WorkerServiceCollectionExtensions.RunConsumersKey] = "false",
            });

            hostedServices.Should().Contain(typeof(OutboxRelay));
            hostedServices.Should().NotContain(Consumers);
        }

        [Fact]
        public void ConsumersOnly_RegistersEveryConsumerButNotTheRelay()
        {
            var hostedServices = HostedServicesFor(new Dictionary<string, string?>
            {
                [WorkerServiceCollectionExtensions.RunOutboxRelayKey] = "false",
            });

            hostedServices.Should().Contain(Consumers);
            hostedServices.Should().NotContain(typeof(OutboxRelay));
        }

        [Theory]
        [InlineData(WorkerServiceCollectionExtensions.RunOutboxRelayKey)]
        [InlineData(WorkerServiceCollectionExtensions.RunConsumersKey)]
        public void EveryRole_RegistersEveryConsumerQueue_ForTheTopologyInitializerToDeclare(string disabledRole)
        {
            var services = CreateServices(new Dictionary<string, string?> { [disabledRole] = "false" });

            var queues = services
                .Where(d => !d.IsKeyedService && d.ServiceType == typeof(ConsumerQueue))
                .Select(d => d.ImplementationInstance);

            queues.Should().BeEquivalentTo(new[]
            {
                TransactionAddedCategorizationConsumer.Queue,
                ImportRequestedConsumer.Queue,
            });
        }

        [Fact]
        public void BothRolesOff_Throws()
        {
            var act = () => CreateServices(new Dictionary<string, string?>
            {
                [WorkerServiceCollectionExtensions.RunOutboxRelayKey] = "false",
                [WorkerServiceCollectionExtensions.RunConsumersKey] = "false",
            });

            act.Should().Throw<InvalidOperationException>()
                .WithMessage("*both false*");
        }

        private static List<Type?> HostedServicesFor(Dictionary<string, string?> roleSettings) =>
            CreateServices(roleSettings)
                .Where(d => !d.IsKeyedService && d.ServiceType == typeof(IHostedService))
                .Select(d => d.ImplementationType)
                .ToList();

        private static ServiceCollection CreateServices(Dictionary<string, string?> roleSettings)
        {
            // Values that only have to exist: nothing here is resolved, so
            // nothing ever connects to them.
            var settings = new Dictionary<string, string?>
            {
                ["ConnectionStrings:FinanceTracker"] = "Host=localhost;Database=unused;Username=unused;Password=unused",
                ["RabbitMq:UserName"] = "unused",
                ["RabbitMq:Password"] = "unused",
            };

            foreach (var (key, value) in roleSettings)
                settings[key] = value;

            var configuration = new ConfigurationBuilder().AddInMemoryCollection(settings).Build();

            var services = new ServiceCollection();
            services.AddWorker(configuration);
            return services;
        }
    }
}
