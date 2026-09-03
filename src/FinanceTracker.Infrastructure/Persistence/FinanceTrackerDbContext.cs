using Microsoft.EntityFrameworkCore;

namespace FinanceTracker.Infrastructure.Persistence
{
    /// <summary>
    /// EF Core DbContext for the Personal Finance Tracking bounded context.
    /// Intentionally has no DbSets yet — aggregates are added one at a time,
    /// each with its own IEntityTypeConfiguration and migration, as
    /// Infrastructure's repository implementations are built out.
    /// </summary>
    public sealed class FinanceTrackerDbContext : DbContext
    {
        public FinanceTrackerDbContext(DbContextOptions<FinanceTrackerDbContext> options)
            : base(options)
        {
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            modelBuilder.ApplyConfigurationsFromAssembly(typeof(FinanceTrackerDbContext).Assembly);
        }
    }
}
