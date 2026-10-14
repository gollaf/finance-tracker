using FinanceTracker.Domain.Categories;
using FinanceTracker.Domain.Common;
using FinanceTracker.Infrastructure.Persistence;
using FinanceTracker.Infrastructure.Persistence.Repositories;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Testcontainers.PostgreSql;

namespace FinanceTracker.Infrastructure.IntegrationTests.Persistence.Repositories
{
    /// <summary>Each test gets its own Postgres container: slow, but fully isolated.</summary>
    public sealed class CategoryRepositoryTests : IAsyncLifetime
    {
        private static readonly UserId Owner = new(Guid.NewGuid());
        private static readonly UserId Stranger = new(Guid.NewGuid());
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
            var parent = Category.Create(Owner, "Food");
            await _repository.AddAsync(parent, CancellationToken.None);

            var child = Category.Create(Owner, "Dining Out", parent.Id);
            await _repository.AddAsync(child, CancellationToken.None);

            var loaded = await _repository.GetByIdAsync(Owner, child.Id, CancellationToken.None);

            loaded.Should().NotBeNull();
            loaded!.Id.Should().Be(child.Id);
            loaded.OwnerId.Should().Be(Owner);
            loaded.Name.Should().Be("Dining Out");
            loaded.ParentCategoryId.Should().Be(parent.Id);
        }

        [Fact]
        public async Task AddAsync_WithoutParent_RoundTripsNullParentCategoryId()
        {
            var category = Category.Create(Owner, "Groceries");
            await _repository.AddAsync(category, CancellationToken.None);

            var loaded = await _repository.GetByIdAsync(Owner, category.Id, CancellationToken.None);

            loaded!.ParentCategoryId.Should().BeNull();
        }

        [Fact]
        public async Task GetByIdAsync_WithUnknownId_ReturnsNull()
        {
            var loaded = await _repository.GetByIdAsync(Owner, CategoryId.New(), CancellationToken.None);

            loaded.Should().BeNull();
        }

        [Theory]
        [InlineData("groceries")]
        [InlineData("GROCERIES")]
        [InlineData("Groceries")]
        public async Task ExistsWithNameAsync_IsCaseInsensitive(string lookupName)
        {
            var category = Category.Create(Owner, "Groceries");
            await _repository.AddAsync(category, CancellationToken.None);

            var exists = await _repository.ExistsWithNameAsync(Owner, lookupName, CancellationToken.None);

            exists.Should().BeTrue();
        }

        [Fact]
        public async Task GetAllAsync_ReturnsEveryCategoryOrderedByName()
        {
            await _repository.AddAsync(Category.Create(Owner, "Transport"), CancellationToken.None);
            await _repository.AddAsync(Category.Create(Owner, "Dining"), CancellationToken.None);
            await _repository.AddAsync(Category.Create(Owner, "Groceries"), CancellationToken.None);

            var all = await _repository.GetAllAsync(Owner, CancellationToken.None);

            all.Select(c => c.Name).Should().Equal("Dining", "Groceries", "Transport");
        }

        [Fact]
        public async Task ExistsWithNameAsync_WithNoMatch_ReturnsFalse()
        {
            var exists = await _repository.ExistsWithNameAsync(Owner, "Nonexistent", CancellationToken.None);

            exists.Should().BeFalse();
        }

        [Fact]
        public async Task GetByIdAsync_ForAnotherOwner_ReturnsNull()
        {
            var category = Category.Create(Owner, "Groceries");
            await _repository.AddAsync(category, CancellationToken.None);

            var loaded = await _repository.GetByIdAsync(Stranger, category.Id, CancellationToken.None);

            loaded.Should().BeNull();
        }

        [Fact]
        public async Task ExistsWithNameAsync_ForAnotherOwner_ReturnsFalse()
        {
            await _repository.AddAsync(Category.Create(Owner, "Groceries"), CancellationToken.None);

            var exists = await _repository.ExistsWithNameAsync(Stranger, "Groceries", CancellationToken.None);

            exists.Should().BeFalse();
        }

        [Fact]
        public async Task GetAllAsync_ReturnsOnlyTheOwnersCategories()
        {
            await _repository.AddAsync(Category.Create(Owner, "Groceries"), CancellationToken.None);
            await _repository.AddAsync(Category.Create(Stranger, "Rent"), CancellationToken.None);

            var all = await _repository.GetAllAsync(Owner, CancellationToken.None);

            all.Select(c => c.Name).Should().Equal("Groceries");
        }
    }
}
