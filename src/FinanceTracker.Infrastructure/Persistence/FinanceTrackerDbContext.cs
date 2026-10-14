using FinanceTracker.Domain.Accounts;
using FinanceTracker.Domain.Budgets;
using FinanceTracker.Domain.Categories;
using FinanceTracker.Domain.Categorization;
using FinanceTracker.Domain.Imports;
using FinanceTracker.Domain.Transactions;
using FinanceTracker.Infrastructure.Outbox;
using Microsoft.AspNetCore.DataProtection.EntityFrameworkCore;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace FinanceTracker.Infrastructure.Persistence
{
    /// <summary>
    /// EF Core DbContext for the application. Each aggregate's mapping lives
    /// in its own IEntityTypeConfiguration under Configurations/.
    /// </summary>
    /// <remarks>
    /// Also holds the ASP.NET Core Identity user tables and the Data Protection
    /// keys that sign-in tokens are encrypted with, so one database and one
    /// migration history cover everything. IdentityUserContext rather than
    /// IdentityDbContext: the API uses no roles, so it gets no role tables.
    /// </remarks>
    public sealed class FinanceTrackerDbContext
        : IdentityUserContext<IdentityUser<Guid>, Guid>, IDataProtectionKeyContext
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

        /// <summary>
        /// Shared by every Api instance, so a token issued by one is readable by
        /// all of them, and still valid after a restart.
        /// </summary>
        public DbSet<DataProtectionKey> DataProtectionKeys => Set<DataProtectionKey>();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            modelBuilder.ApplyConfigurationsFromAssembly(typeof(FinanceTrackerDbContext).Assembly);
        }
    }
}
