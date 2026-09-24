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
    /// Composition root for this layer: registers FinanceTrackerDbContext
    /// and every repository implementation against the Application-layer
    /// interface it satisfies. Mirrors FinanceTracker.Application's own
    /// AddApplication() extension method, called the same way from
    /// Api/Program.cs.
    /// </summary>
    public static class DependencyInjection
    {
        public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
        {
            var connectionString = configuration.GetConnectionString("FinanceTracker")
                ?? throw new InvalidOperationException(
                    "Connection string 'FinanceTracker' was not found. Set it via User Secrets " +
                    "in development (see the Step 4 setup notes) -- it must never be committed " +
                    "to appsettings.json.");

            services.AddDbContext<FinanceTrackerDbContext>(options => options.UseNpgsql(connectionString));

            // Scoped, not Singleton: each repository holds a reference to
            // FinanceTrackerDbContext, and AddDbContext registers that as
            // Scoped (one instance per HTTP request) by default. A
            // Singleton repository would capture a DbContext instance from
            // whichever request created it first and keep reusing it
            // forever -- a classic and hard-to-diagnose bug.
            services.AddScoped<IAccountRepository, AccountRepository>();
            services.AddScoped<ICategoryRepository, CategoryRepository>();
            services.AddScoped<ICategorizationRuleRepository, CategorizationRuleRepository>();
            services.AddScoped<IBudgetRepository, BudgetRepository>();
            services.AddScoped<ITransactionRepository, TransactionRepository>();
            services.AddScoped<IImportJobRepository, ImportJobRepository>();

            // Scoped, like the repositories: its database transaction has to
            // be opened on the same DbContext they save through.
            services.AddScoped<IUnitOfWork, EfCoreUnitOfWork>();

            // Scoped for the same reason as the repositories: it has to
            // share the one FinanceTrackerDbContext of the current request
            // (or message) with them -- that shared context is what makes
            // an outbox row and the change it describes commit together.
            services.AddScoped<IOutbox, EfCoreOutbox>();

            // GroqOptions.ApiKey is intentionally allowed to bind empty --
            // GroqInsightsGenerator treats a missing key as a non-fatal
            // Result.Failure, not a startup crash. See
            // docs/adr/0010-ai-insights-provider-and-integration-design.md.
            services.AddOptions<GroqOptions>()
                .Bind(configuration.GetSection(GroqOptions.SectionName));

            // A typed HttpClient, not a bare "new HttpClient()" inside
            // GroqInsightsGenerator: AddHttpClient hands out pooled,
            // reused HttpMessageHandlers instead of one per instance,
            // which avoids the socket-exhaustion problem a
            // manually-constructed HttpClient is famous for under load.
            // The configure callback reads GroqOptions back out of the
            // same IServiceProvider building this client, so
            // TimeoutSeconds only has to be set in one place.
            services.AddHttpClient<IInsightsGenerator, GroqInsightsGenerator>((serviceProvider, client) =>
            {
                var groqOptions = serviceProvider.GetRequiredService<IOptions<GroqOptions>>().Value;
                client.BaseAddress = new Uri("https://api.groq.com/");
                client.Timeout = TimeSpan.FromSeconds(groqOptions.TimeoutSeconds);
            });

            // Same Groq endpoint, options, and timeout as above; a separate
            // typed client because it's a separate port. See
            // docs/adr/0014-ai-transaction-categorization.md.
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
        /// publisher, a startup step that declares the shared exchanges, and
        /// the OutboxRelay that publishes stored integration events. The
        /// relay reads the outbox through FinanceTrackerDbContext, so this
        /// must be called together with AddInfrastructure.
        /// Separate from AddInfrastructure on purpose -- only a process that
        /// actually talks to the broker (the Worker) calls this. The Api
        /// never does: it only records events in the database, and never
        /// needs RabbitMQ configuration or a broker connection at all. See
        /// docs/adr/0011-async-messaging-rabbitmq-raw-client.md.
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

            // Singletons: the connection is meant to live as long as the
            // process (see RabbitMqConnectionProvider), and the publisher
            // holds one long-lived channel on top of it.
            services.AddSingleton<RabbitMqConnectionProvider>();
            services.AddSingleton<IMessagePublisher, RabbitMqPublisher>();

            services.AddHostedService<RabbitMqTopologyInitializer>();
            services.AddHostedService<OutboxRelay>();

            return services;
        }
    }
}
