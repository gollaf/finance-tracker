using FinanceTracker.Application;
using FinanceTracker.Application.Accounts;
using FinanceTracker.Application.Budgets;
using FinanceTracker.Application.Categories;
using FinanceTracker.Application.Categorization;
using FinanceTracker.Application.Common;
using FinanceTracker.Application.Common.IntegrationEvents;
using FinanceTracker.Application.Imports;
using FinanceTracker.Application.Transactions;
using FinanceTracker.Infrastructure;
using FinanceTracker.Infrastructure.Ai;
using FinanceTracker.Infrastructure.Messaging;
using FinanceTracker.Infrastructure.Outbox;
using FinanceTracker.Infrastructure.Persistence;
using FinanceTracker.Infrastructure.Persistence.Repositories;
using FluentAssertions;
using MediatR;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace FinanceTracker.Infrastructure.IntegrationTests
{
    /// <summary>
    /// Verifies AddApplication() + AddInfrastructure() together produce a
    /// working DI container -- the same composition Api/Program.cs performs
    /// -- without needing a real, reachable database. Registering
    /// FinanceTrackerDbContext with a connection string doesn't open a
    /// connection; that only happens on the first real query. So this test
    /// can use a syntactically valid but nonexistent connection string and
    /// still prove every repository resolves, with no Docker/Testcontainers
    /// needed -- unlike everything else in this project, it runs in
    /// milliseconds.
    ///
    /// Scope: this proves every repository interface and IMediator itself
    /// resolve correctly. It does not individually construct every
    /// command/query handler through DI (that would mean reflecting over
    /// every IRequestHandler&lt;,&gt; registration) -- each handler is
    /// already exercised directly, wired with NSubstitute mocks, in its own
    /// Application unit test. Worth revisiting only if the handler count
    /// grows large enough that a registration mistake there becomes a real,
    /// separate risk.
    /// </summary>
    public sealed class CompositionRootTests
    {
        private static ServiceProvider BuildServiceProvider()
        {
            var configuration = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["ConnectionStrings:FinanceTracker"] =
                        "Host=localhost;Database=does-not-exist;Username=postgres;Password=postgres",
                })
                .Build();

            var services = new ServiceCollection();
            services
                .AddApplication()
                .AddInfrastructure(configuration);

            // validateScopes: true reproduces ASP.NET Core's own default
            // behavior in Development -- it throws at resolution time if a
            // longer-lived service (e.g. Singleton) ends up capturing a
            // shorter-lived one (e.g. our Scoped repositories/DbContext), a
            // real and otherwise-silent bug class.
            return services.BuildServiceProvider(validateScopes: true);
        }

        [Theory]
        [InlineData(typeof(IAccountRepository), typeof(AccountRepository))]
        [InlineData(typeof(ICategoryRepository), typeof(CategoryRepository))]
        [InlineData(typeof(ICategorizationRuleRepository), typeof(CategorizationRuleRepository))]
        [InlineData(typeof(IBudgetRepository), typeof(BudgetRepository))]
        [InlineData(typeof(ITransactionRepository), typeof(TransactionRepository))]
        [InlineData(typeof(IInsightsGenerator), typeof(GroqInsightsGenerator))]
        [InlineData(typeof(IOutbox), typeof(EfCoreOutbox))]
        [InlineData(typeof(ICategorySuggester), typeof(GroqCategorySuggester))]
        [InlineData(typeof(IImportJobRepository), typeof(ImportJobRepository))]
        [InlineData(typeof(IUnitOfWork), typeof(EfCoreUnitOfWork))]
        public void ServiceProvider_ResolvesEachRepository_ToItsInfrastructureImplementation(
            Type serviceType, Type expectedImplementationType)
        {
            using var provider = BuildServiceProvider();
            using var scope = provider.CreateScope();

            var resolved = scope.ServiceProvider.GetService(serviceType);

            resolved.Should().NotBeNull();
            resolved.Should().BeOfType(expectedImplementationType);
        }

        [Fact]
        public void ServiceProvider_ResolvesIMediator()
        {
            using var provider = BuildServiceProvider();
            using var scope = provider.CreateScope();

            var mediator = scope.ServiceProvider.GetService<IMediator>();

            mediator.Should().NotBeNull();
        }

        /// <summary>
        /// AddRabbitMqMessaging registers without connecting: nothing touches
        /// the network until something first asks RabbitMqConnectionProvider
        /// for a connection, so -- like the database registrations above --
        /// this runs with no broker at all.
        /// </summary>
        [Fact]
        public async Task AddRabbitMqMessaging_RegistersPublisherTopologyInitializerAndOutboxRelay()
        {
            var configuration = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["RabbitMq:UserName"] = "financetracker",
                    ["RabbitMq:Password"] = "financetracker",
                })
                .Build();

            var services = new ServiceCollection();
            services.AddLogging();
            services.AddRabbitMqMessaging(configuration);

            // await using, not using: RabbitMqPublisher and
            // RabbitMqConnectionProvider only implement IAsyncDisposable, and
            // a synchronous ServiceProvider.Dispose() throws when it reaches
            // a singleton like that.
            await using var provider = services.BuildServiceProvider(validateScopes: true);

            provider.GetService<IMessagePublisher>().Should().BeOfType<RabbitMqPublisher>();
            provider.GetService<RabbitMqConnectionProvider>().Should().NotBeNull();
            provider.GetServices<IHostedService>().Should().ContainSingle(s => s is RabbitMqTopologyInitializer);
            provider.GetServices<IHostedService>().Should().ContainSingle(s => s is OutboxRelay);
        }
    }
}
