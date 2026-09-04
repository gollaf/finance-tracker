using FinanceTracker.Domain.Categories;
using FinanceTracker.Domain.Common;
using FinanceTracker.Infrastructure.Persistence;
using FinanceTracker.Infrastructure.Persistence.Repositories;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Testcontainers.PostgreSql;

namespace FinanceTracker.Infrastructure.IntegrationTests.Persistence.Repositories
{
    /// <summary>
    /// Each test method gets its own fresh Postgres container (xUnit creates
    /// a new CategoryRepositoryTests instance per [Fact]/[Theory] case, and
    /// IAsyncLifetime runs around each one) — simplest possible isolation,
    /// zero risk of one test's data leaking into another. It's also the
    /// slowest possible option, since every test pays a full container
    /// startup. Fine while there's one repository and a handful of tests;
    /// worth revisiting with a shared container + reset-between-tests
    /// approach once more aggregates make that cost noticeable.
    /// </summary>
    public sealed class CategoryRepositoryTests : IAsyncLifetime
    {
        private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder("postgres:16-alpine")
            .Build();

        private FinanceTrackerDbContext _context = null!;
        private CategoryRepository _repository = null!;

        public async Task InitializeAsync()
        {
            await _postgres.StartAsync();

            var optionsBuilder = new DbContextOptionsBuilder<FinanceTrackerDbContext>()
                .UseNpgsql(_postgres.GetConnectionString());

            _context = new FinanceTrackerDbContext(optionsBuilder.Options);
            await _context.Database.MigrateAsync();

            _repository = new CategoryRepository(_context);
        }

        public async Task DisposeAsync()
        {
            await _context.DisposeAsync();
            await _postgres.DisposeAsync();
        }

        [Fact]
        public async Task AddAsync_ThenGetByIdAsync_RoundTripsAllFields()
        {
            var parent = Category.Create("Food");
            await _repository.AddAsync(parent, CancellationToken.None);

            var child = Category.Create("Dining Out", parent.Id);
            await _repository.AddAsync(child, CancellationToken.None);

            var loaded = await _repository.GetByIdAsync(child.Id, CancellationToken.None);

            loaded.Should().NotBeNull();
            loaded!.Id.Should().Be(child.Id);
            loaded.Name.Should().Be("Dining Out");
            loaded.ParentCategoryId.Should().Be(parent.Id);
        }

        [Fact]
        public async Task AddAsync_WithoutParent_RoundTripsNullParentCategoryId()
        {
            var category = Category.Create("Groceries");
            await _repository.AddAsync(category, CancellationToken.None);

            var loaded = await _repository.GetByIdAsync(category.Id, CancellationToken.None);

            loaded!.ParentCategoryId.Should().BeNull();
        }

        [Fact]
        public async Task GetByIdAsync_WithUnknownId_ReturnsNull()
        {
            var loaded = await _repository.GetByIdAsync(CategoryId.New(), CancellationToken.None);

            loaded.Should().BeNull();
        }

        [Theory]
        [InlineData("groceries")]
        [InlineData("GROCERIES")]
        [InlineData("Groceries")]
        public async Task ExistsWithNameAsync_IsCaseInsensitive(string lookupName)
        {
            var category = Category.Create("Groceries");
            await _repository.AddAsync(category, CancellationToken.None);

            var exists = await _repository.ExistsWithNameAsync(lookupName, CancellationToken.None);

            exists.Should().BeTrue();
        }

        [Fact]
        public async Task ExistsWithNameAsync_WithNoMatch_ReturnsFalse()
        {
            var exists = await _repository.ExistsWithNameAsync("Nonexistent", CancellationToken.None);

            exists.Should().BeFalse();
        }
    }
}
