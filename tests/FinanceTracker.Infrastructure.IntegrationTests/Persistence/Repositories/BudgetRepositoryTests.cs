using FinanceTracker.Domain.Budgets;
using FinanceTracker.Domain.Common;
using FinanceTracker.Infrastructure.Persistence;
using FinanceTracker.Infrastructure.Persistence.Repositories;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Testcontainers.PostgreSql;

namespace FinanceTracker.Infrastructure.IntegrationTests.Persistence.Repositories
{
    public sealed class BudgetRepositoryTests : IAsyncLifetime
    {
        private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder("postgres:16-alpine")
            .Build();

        private FinanceTrackerDbContext _context = null!;
        private BudgetRepository _repository = null!;

        public async Task InitializeAsync()
        {
            await _postgres.StartAsync();

            var optionsBuilder = new DbContextOptionsBuilder<FinanceTrackerDbContext>()
                .UseNpgsql(_postgres.GetConnectionString());

            _context = new FinanceTrackerDbContext(optionsBuilder.Options);
            await _context.Database.MigrateAsync();

            _repository = new BudgetRepository(_context);
        }

        public async Task DisposeAsync()
        {
            await _context.DisposeAsync();
            await _postgres.DisposeAsync();
        }

        [Fact]
        public async Task AddAsync_ThenGetByIdAsync_RoundTripsAllFields()
        {
            var categoryId = CategoryId.New();
            var period = BudgetPeriod.Create(2026, 9);
            var limit = Money.Create(500m, "USD");
            var budget = Budget.Create(categoryId, period, limit);

            await _repository.AddAsync(budget, CancellationToken.None);
            var loaded = await _repository.GetByIdAsync(budget.Id, CancellationToken.None);

            loaded.Should().NotBeNull();
            loaded!.Id.Should().Be(budget.Id);
            loaded.CategoryId.Should().Be(categoryId);
            loaded.Period.Should().Be(period);
            loaded.LimitAmount.Should().Be(limit);
        }

        [Fact]
        public async Task GetByIdAsync_WithUnknownId_ReturnsNull()
        {
            var loaded = await _repository.GetByIdAsync(BudgetId.New(), CancellationToken.None);

            loaded.Should().BeNull();
        }

        [Fact]
        public async Task GetByCategoryAndPeriodAsync_WithMatch_ReturnsBudget()
        {
            var categoryId = CategoryId.New();
            var period = BudgetPeriod.Create(2026, 9);
            var budget = Budget.Create(categoryId, period, Money.Create(500m, "USD"));
            await _repository.AddAsync(budget, CancellationToken.None);

            var loaded = await _repository.GetByCategoryAndPeriodAsync(categoryId, period, CancellationToken.None);

            loaded.Should().NotBeNull();
            loaded!.Id.Should().Be(budget.Id);
        }

        [Fact]
        public async Task GetByCategoryAndPeriodAsync_WithDifferentPeriod_ReturnsNull()
        {
            var categoryId = CategoryId.New();
            var budget = Budget.Create(categoryId, BudgetPeriod.Create(2026, 9), Money.Create(500m, "USD"));
            await _repository.AddAsync(budget, CancellationToken.None);

            var loaded = await _repository.GetByCategoryAndPeriodAsync(
                categoryId, BudgetPeriod.Create(2026, 10), CancellationToken.None);

            loaded.Should().BeNull();
        }

        [Fact]
        public async Task UpdateAsync_PersistsChangedLimit()
        {
            var budget = Budget.Create(CategoryId.New(), BudgetPeriod.Create(2026, 9), Money.Create(500m, "USD"));
            await _repository.AddAsync(budget, CancellationToken.None);

            budget.UpdateLimit(Money.Create(750m, "USD"));
            await _repository.UpdateAsync(budget, CancellationToken.None);

            var loaded = await _repository.GetByIdAsync(budget.Id, CancellationToken.None);

            loaded!.LimitAmount.Should().Be(Money.Create(750m, "USD"));
        }
    }
}
