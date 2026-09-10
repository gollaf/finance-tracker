using FinanceTracker.Application.Accounts;
using FinanceTracker.Application.Budgets;
using FinanceTracker.Application.Categories;
using FinanceTracker.Application.Categorization;
using FinanceTracker.Application.Transactions;
using FinanceTracker.Infrastructure.Ai;
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

            return services;
        }
    }
}
