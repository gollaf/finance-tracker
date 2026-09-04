using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace FinanceTracker.Infrastructure.Persistence
{
    /// <summary>
    /// Lets `dotnet ef` commands build a FinanceTrackerDbContext at design
    /// time without needing the Api project's DI container wired up. The
    /// connection string below is never actually connected to —
    /// `dotnet ef migrations add` only needs to know the provider (Npgsql)
    /// to generate migration code. `dotnet ef database update` and the
    /// running app both use the real connection string from configuration
    /// instead, once Infrastructure's DI registration exists.
    /// </summary>
    public sealed class FinanceTrackerDbContextFactory : IDesignTimeDbContextFactory<FinanceTrackerDbContext>
    {
        public FinanceTrackerDbContext CreateDbContext(string[] args)
        {
            var optionsBuilder = new DbContextOptionsBuilder<FinanceTrackerDbContext>();
            optionsBuilder.UseNpgsql("Host=localhost;Database=financetracker;Username=postgres;Password=postgres");

            return new FinanceTrackerDbContext(optionsBuilder.Options);
        }
    }
}
