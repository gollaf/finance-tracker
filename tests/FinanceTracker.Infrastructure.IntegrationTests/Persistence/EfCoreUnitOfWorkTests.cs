using FinanceTracker.Domain.Accounts;
using FinanceTracker.Domain.Categories;
using FinanceTracker.Infrastructure.Persistence;
using FinanceTracker.Infrastructure.Persistence.Repositories;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Testcontainers.PostgreSql;

namespace FinanceTracker.Infrastructure.IntegrationTests.Persistence
{
    /// <summary>
    /// The two behaviors EfCoreUnitOfWork exists for, against a real
    /// database: several repository saves inside one operation are
    /// committed together, and an exception rolls back every save that
    /// already "succeeded" before it.
    /// </summary>
    public sealed class EfCoreUnitOfWorkTests : IAsyncLifetime
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

        [Fact]
        public async Task ExecuteAtomicallyAsync_WhenOperationSucceeds_CommitsEverySave()
        {
            await using (var context = new FinanceTrackerDbContext(_options))
            {
                var unitOfWork = new EfCoreUnitOfWork(context);

                await unitOfWork.ExecuteAtomicallyAsync(async cancellationToken =>
                {
                    await new AccountRepository(context).AddAsync(
                        Account.Create("Checking", AccountType.Checking, "USD"), cancellationToken);
                    await new CategoryRepository(context).AddAsync(Category.Create("Groceries"), cancellationToken);
                    return true;
                });
            }

            await using var verify = new FinanceTrackerDbContext(_options);
            (await verify.Accounts.CountAsync()).Should().Be(1);
            (await verify.Categories.CountAsync()).Should().Be(1);
        }

        [Fact]
        public async Task ExecuteAtomicallyAsync_WhenOperationThrows_RollsBackSavesThatAlreadyHappened()
        {
            await using (var context = new FinanceTrackerDbContext(_options))
            {
                var unitOfWork = new EfCoreUnitOfWork(context);

                var act = () => unitOfWork.ExecuteAtomicallyAsync<bool>(async cancellationToken =>
                {
                    // This SaveChangesAsync completes without error...
                    await new AccountRepository(context).AddAsync(
                        Account.Create("Checking", AccountType.Checking, "USD"), cancellationToken);

                    // ...but the operation as a whole doesn't.
                    throw new InvalidOperationException("Simulated crash halfway through.");
                });

                await act.Should().ThrowAsync<InvalidOperationException>();
            }

            await using var verify = new FinanceTrackerDbContext(_options);
            (await verify.Accounts.CountAsync()).Should().Be(0, "the account was saved inside the rolled-back transaction");
        }

        [Fact]
        public async Task ExecuteAtomicallyAsync_Nested_JoinsTheOuterTransaction()
        {
            await using (var context = new FinanceTrackerDbContext(_options))
            {
                var unitOfWork = new EfCoreUnitOfWork(context);

                var act = () => unitOfWork.ExecuteAtomicallyAsync<bool>(async outerToken =>
                {
                    await unitOfWork.ExecuteAtomicallyAsync(async innerToken =>
                    {
                        await new AccountRepository(context).AddAsync(
                            Account.Create("Checking", AccountType.Checking, "USD"), innerToken);
                        return true;
                    }, outerToken);

                    // The inner operation "finished" -- but it only joined the
                    // outer transaction, so this still undoes its save.
                    throw new InvalidOperationException("Outer operation fails after the inner one.");
                });

                await act.Should().ThrowAsync<InvalidOperationException>();
            }

            await using var verify = new FinanceTrackerDbContext(_options);
            (await verify.Accounts.CountAsync()).Should().Be(0);
        }
    }
}
