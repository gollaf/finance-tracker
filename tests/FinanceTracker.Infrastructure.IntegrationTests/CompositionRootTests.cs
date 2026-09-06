using FinanceTracker.Application;
using FinanceTracker.Application.Accounts;
using FinanceTracker.Application.Budgets;
using FinanceTracker.Application.Categories;
using FinanceTracker.Application.Categorization;
using FinanceTracker.Application.Transactions;
using FinanceTracker.Infrastructure;
using FinanceTracker.Infrastructure.Persistence.Repositories;
using FluentAssertions;
using MediatR;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

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
    }
}
