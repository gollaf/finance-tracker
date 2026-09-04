using FinanceTracker.Domain.Accounts;
using FinanceTracker.Domain.Categories;
using FinanceTracker.Domain.Categorization;
using Microsoft.EntityFrameworkCore;

namespace FinanceTracker.Infrastructure.Persistence
{
    /// <summary>
    /// EF Core DbContext for the Personal Finance Tracking bounded context.
    /// Aggregates are added one at a time, each with its own
    /// IEntityTypeConfiguration, DbSet, and migration, as Infrastructure's
    /// repository implementations are built out.
    /// </summary>
    public sealed class FinanceTrackerDbContext : DbContext
    {
        public FinanceTrackerDbContext(DbContextOptions<FinanceTrackerDbContext> options)
            : base(options)
        {
        }

        public DbSet<Account> Accounts => Set<Account>();

        public DbSet<Category> Categories => Set<Category>();

        public DbSet<CategorizationRule> CategorizationRules => Set<CategorizationRule>();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            modelBuilder.ApplyConfigurationsFromAssembly(typeof(FinanceTrackerDbContext).Assembly);
        }
    }
}
