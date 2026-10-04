using FinanceTracker.Domain.Accounts;
using FinanceTracker.Domain.Budgets;
using FinanceTracker.Domain.Categories;
using FinanceTracker.Domain.Categorization;
using FinanceTracker.Domain.Imports;
using FinanceTracker.Domain.Transactions;
using FinanceTracker.Infrastructure.Outbox;
using Microsoft.EntityFrameworkCore;

namespace FinanceTracker.Infrastructure.Persistence
{
    /// <summary>
    /// EF Core DbContext for the application. Each aggregate's mapping lives
    /// in its own IEntityTypeConfiguration under Configurations/.
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

        public DbSet<Budget> Budgets => Set<Budget>();

        public DbSet<Transaction> Transactions => Set<Transaction>();

        public DbSet<ImportJob> ImportJobs => Set<ImportJob>();

        /// <summary>
        /// Not an aggregate: the outbox table lives in this DbContext so an
        /// outbox row and its change are saved in one transaction.
        /// </summary>
        public DbSet<OutboxMessage> OutboxMessages => Set<OutboxMessage>();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            modelBuilder.ApplyConfigurationsFromAssembly(typeof(FinanceTrackerDbContext).Assembly);
        }
    }
}
