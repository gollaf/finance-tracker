using FinanceTracker.Application;
using FinanceTracker.Infrastructure;
using FinanceTracker.Infrastructure.Messaging;
using FinanceTracker.Worker.Consumers;

namespace FinanceTracker.Worker
{
    /// <summary>
    /// Everything the Worker process registers, in one method, so that
    /// Program.cs and the Worker's end-to-end tests build exactly the same
    /// service graph -- a test can't accidentally pass against wiring that
    /// differs from what actually runs.
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
        /// Both default to true, so a single Worker (docker compose,
        /// `dotnet run`, the tests) does everything. A deployment that scales
        /// consumers runs one process with only the relay and any number with
        /// only the consumers -- see docs/adr/0018-worker-roles.md.
        ///
        /// Read here, at registration time, rather than through IOptions:
        /// they decide which hosted services exist at all, which has to be
        /// known before the container is built.
        /// </remarks>
        public static IServiceCollection AddWorker(this IServiceCollection services, IConfiguration configuration)
        {
            var runOutboxRelay = configuration.GetValue(RunOutboxRelayKey, defaultValue: true);
            var runConsumers = configuration.GetValue(RunConsumersKey, defaultValue: true);

            // A Worker with both switched off would start, connect, and then
            // sit there doing nothing -- in a deployment, a silently useless
            // process. Refusing to start makes the mistake impossible to miss.
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

            // Registered whatever this process's role is: every Worker
            // declares every consumer's queue at startup, so a relay running
            // on its own never publishes into a routing key with no queue
            // bound to it -- even while no consumer is running anywhere.
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
