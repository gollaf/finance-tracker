using FinanceTracker.Application.Accounts;
using FinanceTracker.Application.Budgets;
using FinanceTracker.Application.Categories;
using FinanceTracker.Application.Categorization;
using FinanceTracker.Application.Common;
using FinanceTracker.Application.Common.IntegrationEvents;
using FinanceTracker.Application.Imports;
using FinanceTracker.Application.Transactions;
using FinanceTracker.Infrastructure.Ai;
using FinanceTracker.Infrastructure.Messaging;
using FinanceTracker.Infrastructure.Outbox;
using FinanceTracker.Infrastructure.Persistence;
using FinanceTracker.Infrastructure.Persistence.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace FinanceTracker.Infrastructure
{
    /// <summary>
    /// Registers this layer's implementations of the Application ports.
    /// </summary>
    public static class DependencyInjection
    {
        public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
        {
            var connectionString = configuration.GetConnectionString("FinanceTracker")
                ?? throw new InvalidOperationException(
                    "Connection string 'FinanceTracker' is not configured. Set " +
                    "ConnectionStrings:FinanceTracker through User Secrets or the " +
                    "ConnectionStrings__FinanceTracker environment variable -- never in " +
                    "appsettings.json.");

            services.AddDbContext<FinanceTrackerDbContext>(options => options.UseNpgsql(connectionString));

            services.AddScoped<IAccountRepository, AccountRepository>();
            services.AddScoped<ICategoryRepository, CategoryRepository>();
            services.AddScoped<ICategorizationRuleRepository, CategorizationRuleRepository>();
            services.AddScoped<IBudgetRepository, BudgetRepository>();
            services.AddScoped<ITransactionRepository, TransactionRepository>();
            services.AddScoped<IImportJobRepository, ImportJobRepository>();

            // Scoped, like the repositories: its database transaction has to
            // be opened on the same DbContext they save through.
            services.AddScoped<IUnitOfWork, EfCoreUnitOfWork>();

            // Shares the scope's DbContext with the repositories, which is
            // what makes an outbox row commit together with its change.
            services.AddScoped<IOutbox, EfCoreOutbox>();

            // No validation: an empty ApiKey is allowed and handled at call time.
            services.AddOptions<GroqOptions>()
                .Bind(configuration.GetSection(GroqOptions.SectionName));

            services.AddHttpClient<IInsightsGenerator, GroqInsightsGenerator>((serviceProvider, client) =>
            {
                var groqOptions = serviceProvider.GetRequiredService<IOptions<GroqOptions>>().Value;
                client.BaseAddress = new Uri("https://api.groq.com/");
                client.Timeout = TimeSpan.FromSeconds(groqOptions.TimeoutSeconds);
            });

            services.AddHttpClient<ICategorySuggester, GroqCategorySuggester>((serviceProvider, client) =>
            {
                var groqOptions = serviceProvider.GetRequiredService<IOptions<GroqOptions>>().Value;
                client.BaseAddress = new Uri("https://api.groq.com/");
                client.Timeout = TimeSpan.FromSeconds(groqOptions.TimeoutSeconds);
            });

            return services;
        }

        /// <summary>
        /// Registers the RabbitMQ plumbing: one shared connection, the
        /// publisher, and a startup step that declares the exchanges and every
        /// registered ConsumerQueue. Only the Worker calls this; the Api just
        /// writes events to the outbox and never connects to the broker.
        /// </summary>
        public static IServiceCollection AddRabbitMqMessaging(this IServiceCollection services, IConfiguration configuration)
        {
            // ValidateOnStart: missing credentials stop the host at startup
            // with this message, instead of surfacing later as a confusing
            // authentication failure deep inside the connection retry loop.
            services.AddOptions<RabbitMqOptions>()
                .Bind(configuration.GetSection(RabbitMqOptions.SectionName))
                .Validate(
                    options => !string.IsNullOrWhiteSpace(options.UserName) && !string.IsNullOrWhiteSpace(options.Password),
                    "RabbitMq:UserName and RabbitMq:Password must be set -- via User Secrets for `dotnet run`, " +
                    "or the RabbitMq__UserName / RabbitMq__Password environment variables in docker-compose.yml.")
                .ValidateOnStart();

            // Singletons: one long-lived connection, and one long-lived
            // publisher channel on top of it.
            services.AddSingleton<RabbitMqConnectionProvider>();
            services.AddSingleton<IMessagePublisher, RabbitMqPublisher>();

            services.AddHostedService<RabbitMqTopologyInitializer>();

            return services;
        }

        /// <summary>
        /// Registers the OutboxRelay, which publishes stored integration
        /// events; it needs AddInfrastructure and AddRabbitMqMessaging too.
        /// Separate because exactly one process should run the relay, while
        /// consumers can run in many. See docs/adr/0018-worker-roles.md.
        /// </summary>
        public static IServiceCollection AddOutboxRelay(this IServiceCollection services)
        {
            services.AddHostedService<OutboxRelay>();

            return services;
        }
    }
}
