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
    /// AddApplication() + AddInfrastructure() produce a working DI container.
    /// Registering the DbContext doesn't open a connection, so a fake
    /// connection string is enough and no container is needed.
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

            // Like ASP.NET Core in Development: fail if a Singleton captures
            // a Scoped service.
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
        /// AddRabbitMqMessaging doesn't connect at registration, so no broker
        /// is needed.
        /// </summary>
        [Fact]
        public async Task AddRabbitMqMessaging_RegistersPublisherAndTopologyInitializer_ButNotTheOutboxRelay()
        {
            var services = CreateMessagingServices();

            // await using: some singletons are only IAsyncDisposable.
            await using var provider = services.BuildServiceProvider(validateScopes: true);

            provider.GetService<IMessagePublisher>().Should().BeOfType<RabbitMqPublisher>();
            provider.GetService<RabbitMqConnectionProvider>().Should().NotBeNull();
            provider.GetServices<IHostedService>().Should().ContainSingle(s => s is RabbitMqTopologyInitializer);
            provider.GetServices<IHostedService>().Should().NotContain(s => s is OutboxRelay);
        }

        /// <summary>
        /// Checks the registration only, not a resolved instance: resolving
        /// OutboxRelay would also need everything AddInfrastructure registers.
        /// </summary>
        [Fact]
        public void AddOutboxRelay_RegistersTheRelayAsAHostedService()
        {
            var services = new ServiceCollection();

            services.AddOutboxRelay();

            services.Should().ContainSingle(d =>
                !d.IsKeyedService && d.ServiceType == typeof(IHostedService) && d.ImplementationType == typeof(OutboxRelay));
        }

        private static ServiceCollection CreateMessagingServices()
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
            return services;
        }
    }
}
