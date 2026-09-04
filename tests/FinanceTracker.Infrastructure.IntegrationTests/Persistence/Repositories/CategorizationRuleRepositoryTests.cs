using FinanceTracker.Domain.Categories;
using FinanceTracker.Domain.Categorization;
using FinanceTracker.Domain.Common;
using FinanceTracker.Infrastructure.Persistence;
using FinanceTracker.Infrastructure.Persistence.Repositories;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Testcontainers.PostgreSql;

namespace FinanceTracker.Infrastructure.IntegrationTests.Persistence.Repositories
{
    public sealed class CategorizationRuleRepositoryTests : IAsyncLifetime
    {
        private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder("postgres:16-alpine")
            .Build();

        private FinanceTrackerDbContext _context = null!;
        private CategorizationRuleRepository _repository = null!;
        private CategoryRepository _categoryRepository = null!;

        public async Task InitializeAsync()
        {
            await _postgres.StartAsync();

            var optionsBuilder = new DbContextOptionsBuilder<FinanceTrackerDbContext>()
                .UseNpgsql(_postgres.GetConnectionString());

            _context = new FinanceTrackerDbContext(optionsBuilder.Options);
            await _context.Database.MigrateAsync();

            _repository = new CategorizationRuleRepository(_context);
            _categoryRepository = new CategoryRepository(_context);
        }

        public async Task DisposeAsync()
        {
            await _context.DisposeAsync();
            await _postgres.DisposeAsync();
        }

        // CategorizationRule.CategoryId now has a real foreign key onto
        // Categories (ADR 0005), so every test that persists a rule needs a
        // Category row that actually exists first — CategoryId.New() alone
        // is no longer enough, the database will reject it.
        private async Task<CategoryId> CreatePersistedCategoryAsync(string name = "Food")
        {
            var category = Category.Create(name);
            await _categoryRepository.AddAsync(category, CancellationToken.None);
            return category.Id;
        }

        [Fact]
        public async Task AddAsync_ThenGetAllAsync_RoundTripsAllFields()
        {
            var categoryId = await CreatePersistedCategoryAsync();
            var rule = CategorizationRule.Create("starbucks", categoryId, priority: 1);

            await _repository.AddAsync(rule, CancellationToken.None);
            var rules = await _repository.GetAllAsync(CancellationToken.None);

            rules.Should().ContainSingle();
            var loaded = rules[0];
            loaded.Id.Should().Be(rule.Id);
            loaded.Pattern.Should().Be("starbucks");
            loaded.CategoryId.Should().Be(categoryId);
            loaded.Priority.Should().Be(1);
        }

        [Fact]
        public async Task GetAllAsync_WithMultipleRules_ReturnsAllOfThem()
        {
            var categoryId = await CreatePersistedCategoryAsync();

            await _repository.AddAsync(CategorizationRule.Create("starbucks", categoryId, 1), CancellationToken.None);
            await _repository.AddAsync(CategorizationRule.Create("uber", categoryId, 2), CancellationToken.None);
            await _repository.AddAsync(CategorizationRule.Create("netflix", categoryId, 3), CancellationToken.None);

            var rules = await _repository.GetAllAsync(CancellationToken.None);

            rules.Should().HaveCount(3);
        }

        [Fact]
        public async Task GetAllAsync_WithNoRules_ReturnsEmpty()
        {
            var rules = await _repository.GetAllAsync(CancellationToken.None);

            rules.Should().BeEmpty();
        }
    }
}
