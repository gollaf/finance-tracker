using FinanceTracker.Application;
using FinanceTracker.Infrastructure;
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
        public static IServiceCollection AddWorker(this IServiceCollection services, IConfiguration configuration)
        {
            services
                .AddApplication()
                .AddInfrastructure(configuration)
                .AddRabbitMqMessaging(configuration);

            services.AddHostedService<TransactionAddedCategorizationConsumer>();

            return services;
        }
    }
}
