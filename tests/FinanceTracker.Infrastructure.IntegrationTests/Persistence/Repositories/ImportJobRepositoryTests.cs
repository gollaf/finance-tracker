using FinanceTracker.Domain.Accounts;
using FinanceTracker.Domain.Common;
using FinanceTracker.Domain.Imports;
using FinanceTracker.Domain.Transactions;
using FinanceTracker.Infrastructure.Persistence;
using FinanceTracker.Infrastructure.Persistence.Repositories;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Testcontainers.PostgreSql;

namespace FinanceTracker.Infrastructure.IntegrationTests.Persistence.Repositories
{
    public sealed class ImportJobRepositoryTests : IAsyncLifetime
    {
        private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder("postgres:16-alpine")
            .Build();

        private DbContextOptions<FinanceTrackerDbContext> _options = null!;

        public async Task InitializeAsync()
        {
            await _postgres.StartAsync();

            _options = new DbContextOptionsBuilder<FinanceTrackerDbContext>()
                .UseNpgsql(_postgres.GetConnectionString())
                .Options;

            await using var context = new FinanceTrackerDbContext(_options);
            await context.Database.MigrateAsync();
        }

        public Task DisposeAsync() => _postgres.DisposeAsync().AsTask();

        private async Task<AccountId> CreatePersistedAccountAsync()
        {
            await using var context = new FinanceTrackerDbContext(_options);
            var account = Account.Create("Checking", AccountType.Checking, "USD");
            await new AccountRepository(context).AddAsync(account, CancellationToken.None);
            return account.Id;
        }

        private static ImportJob NewJob(AccountId accountId) =>
            ImportJob.Create(
                accountId,
                new[]
                {
                    new ImportJobRow(2, 4.5m, TransactionType.Expense, "Coffee", new DateOnly(2026, 9, 1)),
                    new ImportJobRow(4, 1200m, TransactionType.Income, "Salary", new DateOnly(2026, 9, 2)),
                },
                new[] { new ImportJobRowError(3, "Amount is not a number.") });

        [Fact]
        public async Task AddAsync_ThenGetByIdAsync_RoundTripsRowsAndErrorsThroughJsonb()
        {
            var job = NewJob(await CreatePersistedAccountAsync());

            await using (var context = new FinanceTrackerDbContext(_options))
            {
                await new ImportJobRepository(context).AddAsync(job, CancellationToken.None);
            }

            // A fresh DbContext, so this really reads the database rather
            // than returning the instance the first context still tracks.
            await using var verify = new FinanceTrackerDbContext(_options);
            var loaded = await new ImportJobRepository(verify).GetByIdAsync(job.Id, CancellationToken.None);

            loaded.Should().NotBeNull();
            loaded!.AccountId.Should().Be(job.AccountId);
            loaded.Status.Should().Be(ImportJobStatus.Pending);
            loaded.Rows.Should().Equal(job.Rows);
            loaded.Errors.Should().Equal(job.Errors);
            loaded.CreatedAt.Should().BeCloseTo(job.CreatedAt, TimeSpan.FromMilliseconds(1));
        }

        [Fact]
        public async Task UpdateAsync_AfterComplete_PersistsStatusCountAndMergedErrors()
        {
            var job = NewJob(await CreatePersistedAccountAsync());

            await using (var context = new FinanceTrackerDbContext(_options))
            {
                var repository = new ImportJobRepository(context);
                await repository.AddAsync(job, CancellationToken.None);

                job.Complete(1, new[] { new ImportJobRowError(2, "Description is required.") });
                await repository.UpdateAsync(job, CancellationToken.None);
            }

            await using var verify = new FinanceTrackerDbContext(_options);
            var loaded = await new ImportJobRepository(verify).GetByIdAsync(job.Id, CancellationToken.None);

            loaded!.Status.Should().Be(ImportJobStatus.Completed);
            loaded.ImportedCount.Should().Be(1);
            loaded.Errors.Select(e => e.RowNumber).Should().Equal(2, 3);
            loaded.CompletedAt.Should().NotBeNull();
        }

        [Fact]
        public async Task GetByIdAsync_WithUnknownId_ReturnsNull()
        {
            await using var context = new FinanceTrackerDbContext(_options);

            var loaded = await new ImportJobRepository(context).GetByIdAsync(ImportJobId.New(), CancellationToken.None);

            loaded.Should().BeNull();
        }
    }
}
