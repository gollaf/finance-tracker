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

        public async Task InitializeAsync()
        {
            await _postgres.StartAsync();

            var optionsBuilder = new DbContextOptionsBuilder<FinanceTrackerDbContext>()
                .UseNpgsql(_postgres.GetConnectionString());

            _context = new FinanceTrackerDbContext(optionsBuilder.Options);
            await _context.Database.MigrateAsync();

            _repository = new CategorizationRuleRepository(_context);
        }

        public async Task DisposeAsync()
        {
            await _context.DisposeAsync();
            await _postgres.DisposeAsync();
        }

        [Fact]
        public async Task AddAsync_ThenGetAllAsync_RoundTripsAllFields()
        {
            var categoryId = CategoryId.New();
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
            await _repository.AddAsync(CategorizationRule.Create("starbucks", CategoryId.New(), 1), CancellationToken.None);
            await _repository.AddAsync(CategorizationRule.Create("uber", CategoryId.New(), 2), CancellationToken.None);
            await _repository.AddAsync(CategorizationRule.Create("netflix", CategoryId.New(), 3), CancellationToken.None);

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
