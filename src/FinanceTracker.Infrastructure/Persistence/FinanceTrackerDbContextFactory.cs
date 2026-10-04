using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace FinanceTracker.Infrastructure.Persistence
{
    /// <summary>
    /// Lets `dotnet ef` build a FinanceTrackerDbContext at design time. The
    /// running application never uses it.
    /// </summary>
    /// <remarks>
    /// `dotnet ef` always prefers this factory, so `dotnet ef database update`
    /// really connects with the connection string below: a local Postgres
    /// with these credentials.
    /// </remarks>
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
