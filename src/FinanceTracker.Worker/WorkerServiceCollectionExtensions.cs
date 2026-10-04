using FinanceTracker.Application;
using FinanceTracker.Infrastructure;
using FinanceTracker.Infrastructure.Messaging;
using FinanceTracker.Worker.Consumers;

namespace FinanceTracker.Worker
{
    /// <summary>
    /// Everything the Worker registers, in one method, so Program.cs and the
    /// end-to-end tests build the same service graph.
    /// </summary>
    public static class WorkerServiceCollectionExtensions
    {
        public const string RunOutboxRelayKey = "Worker:RunOutboxRelay";
        public const string RunConsumersKey = "Worker:RunConsumers";

        /// <remarks>
        /// The Worker has two independent jobs, and each can be switched off:
        /// <list type="bullet">
        /// <item><c>Worker:RunOutboxRelay</c> -- publish outbox rows. Must run
        /// in exactly one process at a time.</item>
        /// <item><c>Worker:RunConsumers</c> -- handle messages from the
        /// queues. Can run in as many processes as the load needs.</item>
        /// </list>
        /// Both default to true, so a single Worker does everything. See
        /// docs/adr/0018-worker-roles.md. Read at registration time, not via
        /// IOptions, because they decide which hosted services exist.
        /// </remarks>
        public static IServiceCollection AddWorker(this IServiceCollection services, IConfiguration configuration)
        {
            var runOutboxRelay = configuration.GetValue(RunOutboxRelayKey, defaultValue: true);
            var runConsumers = configuration.GetValue(RunConsumersKey, defaultValue: true);

            // Fail loudly rather than run a process that does nothing.
            if (!runOutboxRelay && !runConsumers)
            {
                throw new InvalidOperationException(
                    $"{RunOutboxRelayKey} and {RunConsumersKey} are both false, so this Worker would do nothing. " +
                    "Set at least one of them to true (both default to true).");
            }

            services
                .AddApplication()
                .AddInfrastructure(configuration)
                .AddRabbitMqMessaging(configuration);

            // Every Worker declares every queue, so a relay running alone
            // never publishes to a routing key with no queue bound.
            services.AddSingleton(TransactionAddedCategorizationConsumer.Queue);
            services.AddSingleton(ImportRequestedConsumer.Queue);

            if (runOutboxRelay)
                services.AddOutboxRelay();

            if (runConsumers)
            {
                services.AddHostedService<TransactionAddedCategorizationConsumer>();
                services.AddHostedService<ImportRequestedConsumer>();
            }

            return services;
        }
    }
}
