using System.Globalization;
using FinanceTracker.Application.Accounts.CreateAccount;
using FinanceTracker.Application.Categories.CreateCategory;
using FinanceTracker.Application.Categorization.CreateCategorizationRule;
using FinanceTracker.Application.Common;
using FinanceTracker.Application.Transactions;
using FinanceTracker.Application.Transactions.AddTransaction;
using FinanceTracker.Application.Transactions.ImportTransactionsFromCsv;
using FinanceTracker.Domain.Accounts;
using FinanceTracker.Domain.Common;
using FinanceTracker.Domain.Transactions;
using FinanceTracker.Infrastructure.Persistence;
using FluentAssertions;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Testcontainers.PostgreSql;
using Testcontainers.RabbitMq;

namespace FinanceTracker.Worker.IntegrationTests
{
    /// <summary>
    /// The whole asynchronous categorization pipeline, end to end, with real
    /// Postgres and real RabbitMQ:
    ///
    ///   AddTransaction / ImportTransactionsFromCsv
    ///     -> outbox row (same DB transaction)
    ///     -> OutboxRelay publishes it
    ///     -> RabbitMQ routes it to the categorize queue
    ///     -> TransactionAddedCategorizationConsumer
    ///     -> SuggestCategoryForTransactionCommand
    ///     -> conditional update in Postgres
    ///
    /// The Worker's service graph is built by the same AddWorker method
    /// Program.cs uses, with one substitution: Groq is replaced by
    /// StubCategorySuggester, so the tests are deterministic and need no API
    /// key. Commands are sent through the Worker's own IMediator here purely
    /// as a convenient way to write data; in the running system the Api
    /// sends them, against the same database.
    /// </summary>
    public sealed class CategorizationPipelineTests : IAsyncLifetime
    {
        private const string RabbitMqUser = "financetracker";
        private const string RabbitMqPassword = "financetracker";
        private static readonly DateOnly Today = DateOnly.FromDateTime(DateTime.UtcNow);
        private static readonly TimeSpan WaitTimeout = TimeSpan.FromSeconds(30);

        private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder("postgres:16-alpine")
            .Build();

        private readonly RabbitMqContainer _rabbitMq = new RabbitMqBuilder("rabbitmq:4-management")
            .WithUsername(RabbitMqUser)
            .WithPassword(RabbitMqPassword)
            .Build();

        private readonly StubCategorySuggester _suggester = new("Transport");

        private IHost _host = null!;

        public async Task InitializeAsync()
        {
            await Task.WhenAll(_postgres.StartAsync(), _rabbitMq.StartAsync());

            // Development: turns on DI scope validation and ValidateOnBuild,
            // so a missing registration fails right here, exactly as it
            // would when the real Worker starts.
            var builder = Host.CreateApplicationBuilder(new HostApplicationBuilderSettings
            {
                EnvironmentName = Environments.Development,
            });

            builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:FinanceTracker"] = _postgres.GetConnectionString(),
                ["RabbitMq:Host"] = _rabbitMq.Hostname,
                ["RabbitMq:Port"] = _rabbitMq.GetMappedPublicPort(5672).ToString(CultureInfo.InvariantCulture),
                ["RabbitMq:UserName"] = RabbitMqUser,
                ["RabbitMq:Password"] = RabbitMqPassword,
            });

            builder.Services.AddWorker(builder.Configuration);

            // The one substitution: no real Groq calls from tests.
            builder.Services.RemoveAll<ICategorySuggester>();
            builder.Services.AddSingleton<ICategorySuggester>(_suggester);

            _host = builder.Build();

            // In the running system the Api applies migrations on startup
            // (ADR 0008); there's no Api here, so the test does it.
            await using (var scope = _host.Services.CreateAsyncScope())
            {
                await scope.ServiceProvider.GetRequiredService<FinanceTrackerDbContext>().Database.MigrateAsync();
            }

            await _host.StartAsync();
        }

        public async Task DisposeAsync()
        {
            await _host.StopAsync();

            // Not _host.Dispose(): some singletons (RabbitMqPublisher,
            // RabbitMqConnectionProvider) only implement IAsyncDisposable,
            // and a synchronous Dispose() throws when it reaches them.
            if (_host is IAsyncDisposable asyncDisposable)
                await asyncDisposable.DisposeAsync();
            else
                _host.Dispose();

            await _rabbitMq.DisposeAsync();
            await _postgres.DisposeAsync();
        }

        [Fact]
        public async Task AddedTransaction_IsCategorizedByTheWorker()
        {
            var accountId = await SendAsync(new CreateAccountCommand("Checking", AccountType.Checking, "USD"));
            await SendAsync(new CreateCategoryCommand("Groceries"));
            var transportId = await SendAsync(new CreateCategoryCommand("Transport"));

            var transactionId = await SendAsync(
                new AddTransactionCommand(accountId, 18m, TransactionType.Expense, "UBER *TRIP", Today));

            var categoryId = await WaitForCategoryAsync(transactionId);

            categoryId.Should().Be(transportId);
            _suggester.Calls.Should().Be(1);
        }

        [Fact]
        public async Task TransactionCategorizedByARule_KeepsItsCategory_AndTheAiIsNeverAsked()
        {
            var accountId = await SendAsync(new CreateAccountCommand("Checking", AccountType.Checking, "USD"));
            var groceriesId = await SendAsync(new CreateCategoryCommand("Groceries"));
            var transportId = await SendAsync(new CreateCategoryCommand("Transport"));
            await SendAsync(new CreateCategorizationRuleCommand("supermarket", groceriesId, Priority: 1));

            var import = await SendAsync(new ImportTransactionsFromCsvCommand(accountId, new[]
            {
                new CsvTransactionRow(54m, TransactionType.Expense, "CITY SUPERMARKET", Today),
            }));
            var ruleCategorizedId = import.ImportedTransactionIds.Single();

            // A second, uncategorized transaction, added after the first.
            // One consumer, prefetch 1, FIFO queue: once this one has been
            // categorized, the earlier message has certainly been handled too.
            var uncategorizedId = await SendAsync(
                new AddTransactionCommand(accountId, 18m, TransactionType.Expense, "UBER *TRIP", Today));
            (await WaitForCategoryAsync(uncategorizedId)).Should().Be(transportId);

            (await LoadCategoryAsync(ruleCategorizedId)).Should().Be(groceriesId);
            _suggester.Calls.Should().Be(1, "only the uncategorized transaction should have reached the AI");
        }

        private async Task<TValue> SendAsync<TValue>(IRequest<Result<TValue>> request)
        {
            await using var scope = _host.Services.CreateAsyncScope();
            var result = await scope.ServiceProvider.GetRequiredService<ISender>().Send(request);

            result.IsSuccess.Should().BeTrue($"{request.GetType().Name} should succeed, but failed with: {result.Error.Message}");
            return result.Value;
        }

        private async Task<CategoryId?> LoadCategoryAsync(TransactionId transactionId)
        {
            // A new scope, so a new DbContext, on every read -- a reused one
            // would keep returning its first, stale copy of the row.
            await using var scope = _host.Services.CreateAsyncScope();
            var transaction = await scope.ServiceProvider.GetRequiredService<ITransactionRepository>()
                .GetByIdAsync(transactionId);

            return transaction?.CategoryId;
        }

        /// <summary>Polls until the Transaction has a Category, or WaitTimeout passes.</summary>
        private async Task<CategoryId?> WaitForCategoryAsync(TransactionId transactionId)
        {
            var deadline = DateTime.UtcNow + WaitTimeout;

            while (DateTime.UtcNow < deadline)
            {
                if (await LoadCategoryAsync(transactionId) is { } categoryId)
                    return categoryId;

                await Task.Delay(TimeSpan.FromMilliseconds(250));
            }

            return null;
        }
    }
}
