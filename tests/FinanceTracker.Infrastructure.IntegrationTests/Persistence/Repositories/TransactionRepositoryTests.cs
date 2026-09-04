using FinanceTracker.Domain.Accounts;
using FinanceTracker.Domain.Categories;
using FinanceTracker.Domain.Common;
using FinanceTracker.Domain.Transactions;
using FinanceTracker.Infrastructure.Persistence;
using FinanceTracker.Infrastructure.Persistence.Repositories;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Testcontainers.PostgreSql;

namespace FinanceTracker.Infrastructure.IntegrationTests.Persistence.Repositories
{
    public sealed class TransactionRepositoryTests : IAsyncLifetime
    {
        private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder("postgres:16-alpine")
            .Build();

        private FinanceTrackerDbContext _context = null!;
        private TransactionRepository _repository = null!;
        private AccountRepository _accountRepository = null!;
        private CategoryRepository _categoryRepository = null!;

        public async Task InitializeAsync()
        {
            await _postgres.StartAsync();

            var optionsBuilder = new DbContextOptionsBuilder<FinanceTrackerDbContext>()
                .UseNpgsql(_postgres.GetConnectionString());

            _context = new FinanceTrackerDbContext(optionsBuilder.Options);
            await _context.Database.MigrateAsync();

            _repository = new TransactionRepository(_context);
            _accountRepository = new AccountRepository(_context);
            _categoryRepository = new CategoryRepository(_context);
        }

        public async Task DisposeAsync()
        {
            await _context.DisposeAsync();
            await _postgres.DisposeAsync();
        }

        // Both AccountId and CategoryId now have real foreign keys (ADR
        // 0005), so every test needs real, persisted rows to point at.
        private async Task<AccountId> CreatePersistedAccountAsync(string name = "Checking")
        {
            var account = Account.Create(name, AccountType.Checking, "USD");
            await _accountRepository.AddAsync(account, CancellationToken.None);
            return account.Id;
        }

        private async Task<CategoryId> CreatePersistedCategoryAsync(string name = "Food")
        {
            var category = Category.Create(name);
            await _categoryRepository.AddAsync(category, CancellationToken.None);
            return category.Id;
        }

        [Fact]
        public async Task AddAsync_ThenGetByIdAsync_RoundTripsAllFields()
        {
            var accountId = await CreatePersistedAccountAsync();
            var categoryId = await CreatePersistedCategoryAsync();
            var amount = Money.Create(42.50m, "USD");
            var occurredOn = new DateOnly(2026, 9, 1);
            var transaction = Transaction.Create(
                accountId, amount, TransactionType.Expense, "Groceries", occurredOn, categoryId);

            await _repository.AddAsync(transaction, CancellationToken.None);
            var loaded = await _repository.GetByIdAsync(transaction.Id, CancellationToken.None);

            loaded.Should().NotBeNull();
            loaded!.Id.Should().Be(transaction.Id);
            loaded.AccountId.Should().Be(accountId);
            loaded.CategoryId.Should().Be(categoryId);
            loaded.Amount.Should().Be(amount);
            loaded.Type.Should().Be(TransactionType.Expense);
            loaded.Description.Should().Be("Groceries");
            loaded.OccurredOn.Should().Be(occurredOn);
            loaded.CreatedAt.Should().Be(transaction.CreatedAt);
        }

        [Fact]
        public async Task AddAsync_WithoutCategory_RoundTripsNullCategoryId()
        {
            var accountId = await CreatePersistedAccountAsync();
            var transaction = Transaction.Create(
                accountId, Money.Create(10m, "USD"), TransactionType.Income, "Refund", new DateOnly(2026, 9, 1));

            await _repository.AddAsync(transaction, CancellationToken.None);
            var loaded = await _repository.GetByIdAsync(transaction.Id, CancellationToken.None);

            loaded!.CategoryId.Should().BeNull();
        }

        [Fact]
        public async Task GetByIdAsync_WithUnknownId_ReturnsNull()
        {
            var loaded = await _repository.GetByIdAsync(TransactionId.New(), CancellationToken.None);

            loaded.Should().BeNull();
        }

        [Fact]
        public async Task GetByAccountIdAsync_ReturnsOnlyThatAccountsTransactions()
        {
            var accountId = await CreatePersistedAccountAsync("Checking");
            var otherAccountId = await CreatePersistedAccountAsync("Savings");

            var transaction = Transaction.Create(
                accountId, Money.Create(10m, "USD"), TransactionType.Expense, "Coffee", new DateOnly(2026, 9, 1));
            var otherTransaction = Transaction.Create(
                otherAccountId, Money.Create(20m, "USD"), TransactionType.Expense, "Books", new DateOnly(2026, 9, 1));
            await _repository.AddAsync(transaction, CancellationToken.None);
            await _repository.AddAsync(otherTransaction, CancellationToken.None);

            var results = await _repository.GetByAccountIdAsync(accountId, CancellationToken.None);

            results.Should().ContainSingle();
            results[0].Id.Should().Be(transaction.Id);
        }

        [Fact]
        public async Task GetByCategoryIdAsync_ReturnsMatchingTransactionsAcrossAccounts()
        {
            var accountId = await CreatePersistedAccountAsync("Checking");
            var otherAccountId = await CreatePersistedAccountAsync("Savings");
            var categoryId = await CreatePersistedCategoryAsync();

            var inAccountOne = Transaction.Create(
                accountId, Money.Create(10m, "USD"), TransactionType.Expense, "Coffee", new DateOnly(2026, 9, 1), categoryId);
            var inAccountTwo = Transaction.Create(
                otherAccountId, Money.Create(15m, "USD"), TransactionType.Expense, "Tea", new DateOnly(2026, 9, 2), categoryId);
            var uncategorized = Transaction.Create(
                accountId, Money.Create(20m, "USD"), TransactionType.Expense, "Misc", new DateOnly(2026, 9, 3));
            await _repository.AddAsync(inAccountOne, CancellationToken.None);
            await _repository.AddAsync(inAccountTwo, CancellationToken.None);
            await _repository.AddAsync(uncategorized, CancellationToken.None);

            var results = await _repository.GetByCategoryIdAsync(categoryId, CancellationToken.None);

            results.Should().HaveCount(2);
            results.Select(t => t.Id).Should().BeEquivalentTo([inAccountOne.Id, inAccountTwo.Id]);
        }

        [Fact]
        public async Task UpdateAsync_PersistsChangedAmount()
        {
            var accountId = await CreatePersistedAccountAsync();
            var transaction = Transaction.Create(
                accountId, Money.Create(10m, "USD"), TransactionType.Expense, "Coffee", new DateOnly(2026, 9, 1));
            await _repository.AddAsync(transaction, CancellationToken.None);

            transaction.UpdateAmount(Money.Create(12.5m, "USD"));
            await _repository.UpdateAsync(transaction, CancellationToken.None);

            var loaded = await _repository.GetByIdAsync(transaction.Id, CancellationToken.None);

            loaded!.Amount.Should().Be(Money.Create(12.5m, "USD"));
        }

        [Fact]
        public async Task DeleteAsync_RemovesTransaction()
        {
            var accountId = await CreatePersistedAccountAsync();
            var transaction = Transaction.Create(
                accountId, Money.Create(10m, "USD"), TransactionType.Expense, "Coffee", new DateOnly(2026, 9, 1));
            await _repository.AddAsync(transaction, CancellationToken.None);

            await _repository.DeleteAsync(transaction, CancellationToken.None);

            var loaded = await _repository.GetByIdAsync(transaction.Id, CancellationToken.None);
            loaded.Should().BeNull();
        }
    }
}
