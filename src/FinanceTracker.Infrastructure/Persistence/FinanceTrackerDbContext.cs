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

        public DbSet<Budget> Budgets => Set<Budget>();

        public DbSet<Transaction> Transactions => Set<Transaction>();

        public DbSet<ImportJob> ImportJobs => Set<ImportJob>();

        /// <summary>
        /// Not an aggregate: the transactional outbox's table, living in this
        /// same DbContext on purpose, so an outbox row and the change it
        /// describes are saved by one SaveChangesAsync, in one database
        /// transaction. See docs/adr/0013-transactional-outbox.md.
        /// </summary>
        public DbSet<OutboxMessage> OutboxMessages => Set<OutboxMessage>();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            modelBuilder.ApplyConfigurationsFromAssembly(typeof(FinanceTrackerDbContext).Assembly);
        }
    }
}
