using FinanceTracker.Domain.Accounts;
using FinanceTracker.Domain.Common;
using FinanceTracker.Infrastructure.Persistence;
using FinanceTracker.Infrastructure.Persistence.Repositories;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Testcontainers.PostgreSql;

namespace FinanceTracker.Infrastructure.IntegrationTests.Persistence.Repositories
{
    public sealed class AccountRepositoryTests : IAsyncLifetime
    {
        private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder("postgres:16-alpine")
            .Build();

        private FinanceTrackerDbContext _context = null!;
        private AccountRepository _repository = null!;

        public async Task InitializeAsync()
        {
            await _postgres.StartAsync();

            var optionsBuilder = new DbContextOptionsBuilder<FinanceTrackerDbContext>()
                .UseNpgsql(_postgres.GetConnectionString());

            _context = new FinanceTrackerDbContext(optionsBuilder.Options);
            await _context.Database.MigrateAsync();

            _repository = new AccountRepository(_context);
        }

        public async Task DisposeAsync()
        {
            await _context.DisposeAsync();
            await _postgres.DisposeAsync();
        }

        [Fact]
        public async Task AddAsync_ThenGetByIdAsync_RoundTripsAllFields()
        {
            var account = Account.Create("Checking", AccountType.Checking, "USD");

            await _repository.AddAsync(account, CancellationToken.None);
            var loaded = await _repository.GetByIdAsync(account.Id, CancellationToken.None);

            loaded.Should().NotBeNull();
            loaded!.Id.Should().Be(account.Id);
            loaded.Name.Should().Be("Checking");
            loaded.Type.Should().Be(AccountType.Checking);
            loaded.Currency.Should().Be("USD");
            loaded.IsClosed.Should().BeFalse();
        }

        [Theory]
        [InlineData(AccountType.Checking)]
        [InlineData(AccountType.Savings)]
        [InlineData(AccountType.Credit)]
        [InlineData(AccountType.Cash)]
        [InlineData(AccountType.Investment)]
        public async Task AddAsync_ThenGetByIdAsync_RoundTripsEveryAccountType(AccountType type)
        {
            var account = Account.Create("Test", type, "USD");

            await _repository.AddAsync(account, CancellationToken.None);
            var loaded = await _repository.GetByIdAsync(account.Id, CancellationToken.None);

            loaded!.Type.Should().Be(type);
        }

        [Fact]
        public async Task GetByIdAsync_WithUnknownId_ReturnsNull()
        {
            var loaded = await _repository.GetByIdAsync(AccountId.New(), CancellationToken.None);

            loaded.Should().BeNull();
        }
    }
}
