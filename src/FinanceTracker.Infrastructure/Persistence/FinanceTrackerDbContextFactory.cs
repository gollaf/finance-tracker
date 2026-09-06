using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace FinanceTracker.Infrastructure.Persistence
{
    /// <summary>
    /// Lets `dotnet ef` commands build a FinanceTrackerDbContext at design
    /// time without needing the Api project's DI container wired up.
    ///
    /// Correction to an earlier assumption here: once an
    /// IDesignTimeDbContextFactory implementation exists, EF Core's `dotnet
    /// ef` tooling always prefers it over trying to build the app's real
    /// host from Program.cs -- for every command that needs a
    /// FinanceTrackerDbContext, including `dotnet ef database update`, not
    /// just `dotnet ef migrations add`. So the connection string below
    /// isn't purely a design-time-only placeholder: it's genuinely what
    /// `database update` connects with. For local development, run
    /// Postgres with matching credentials (see the Step 4 piece notes) so
    /// `dotnet ef` commands and this factory agree.
    ///
    /// The running application itself never touches this class: Api's
    /// Program.cs resolves the real connection string from configuration
    /// via Infrastructure's AddInfrastructure(), completely independently
    /// of anything here.
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
