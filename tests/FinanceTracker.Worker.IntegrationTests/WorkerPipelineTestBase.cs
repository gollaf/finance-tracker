using System.Globalization;
using FinanceTracker.Application.Common;
using FinanceTracker.Application.Imports;
using FinanceTracker.Application.Transactions;
using FinanceTracker.Domain.Common;
using FinanceTracker.Domain.Imports;
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
    /// Runs the real Worker -- built by the same AddWorker method Program.cs
    /// uses -- against real Postgres and real RabbitMQ containers, with one
    /// substitution: Groq is replaced by StubCategorySuggester, so tests are
    /// deterministic and need no API key. Commands are sent through the
    /// Worker's own IMediator purely as a convenient way to write data; in
    /// the running system the Api sends them, against the same database.
    /// Each test gets fresh containers (IAsyncLifetime runs per test).
    /// </summary>
    public abstract class WorkerPipelineTestBase : IAsyncLifetime
    {
        protected static readonly DateOnly Today = DateOnly.FromDateTime(DateTime.UtcNow);

        private const string RabbitMqUser = "financetracker";
        private const string RabbitMqPassword = "financetracker";
        private static readonly TimeSpan WaitTimeout = TimeSpan.FromSeconds(30);

        private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder("postgres:16-alpine")
            .Build();

        private readonly RabbitMqContainer _rabbitMq = new RabbitMqBuilder("rabbitmq:4-management")
            .WithUsername(RabbitMqUser)
            .WithPassword(RabbitMqPassword)
            .Build();

        private IHost _host = null!;

        protected StubCategorySuggester Suggester { get; } = new("Transport");

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
            builder.Services.AddSingleton<ICategorySuggester>(Suggester);

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

        protected async Task<TValue> SendAsync<TValue>(IRequest<Result<TValue>> request)
        {
            await using var scope = _host.Services.CreateAsyncScope();
            var result = await scope.ServiceProvider.GetRequiredService<ISender>().Send(request);

            result.IsSuccess.Should().BeTrue($"{request.GetType().Name} should succeed, but failed with: {result.Error.Message}");
            return result.Value;
        }

        // Every read below uses a new scope, so a new DbContext: a reused one
        // would keep returning its first, stale copy of the row.

        protected async Task<CategoryId?> LoadCategoryAsync(TransactionId transactionId)
        {
            await using var scope = _host.Services.CreateAsyncScope();
            var transaction = await scope.ServiceProvider.GetRequiredService<ITransactionRepository>()
                .GetByIdAsync(transactionId);

            return transaction?.CategoryId;
        }

        protected async Task<IReadOnlyList<Transaction>> LoadTransactionsAsync(AccountId accountId)
        {
            await using var scope = _host.Services.CreateAsyncScope();
            return await scope.ServiceProvider.GetRequiredService<ITransactionRepository>()
                .GetByAccountIdAsync(accountId);
        }

        /// <summary>Polls until the Transaction has a Category, or the timeout passes.</summary>
        protected async Task<CategoryId?> WaitForCategoryAsync(TransactionId transactionId)
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

        /// <summary>Polls until the ImportJob is no longer Pending, or the timeout passes.</summary>
        protected async Task<ImportJob> WaitForProcessedImportJobAsync(ImportJobId importJobId)
        {
            var deadline = DateTime.UtcNow + WaitTimeout;

            while (true)
            {
                await using (var scope = _host.Services.CreateAsyncScope())
                {
                    var job = await scope.ServiceProvider.GetRequiredService<IImportJobRepository>()
                        .GetByIdAsync(importJobId);

                    if (job is { IsPending: false } || DateTime.UtcNow >= deadline)
                    {
                        job.Should().NotBeNull();
                        job!.IsPending.Should().BeFalse("the Worker should have processed the import job by now");
                        return job;
                    }
                }

                await Task.Delay(TimeSpan.FromMilliseconds(250));
            }
        }
    }
}
