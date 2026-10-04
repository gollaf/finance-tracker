using FinanceTracker.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace FinanceTracker.Api.HealthChecks
{
    /// <summary>
    /// Readiness check: healthy only once every migration this build knows
    /// about has been applied. When migrations run as a separate step
    /// (ADR 0017), an instance could otherwise report ready before its
    /// tables exist.
    ///
    /// An older instance stays ready after a newer version has migrated
    /// further ahead. That is only safe if each migration stays compatible
    /// with the previous version of the code.
    /// </summary>
    public sealed class PendingMigrationsHealthCheck : IHealthCheck
    {
        private readonly FinanceTrackerDbContext _dbContext;

        public PendingMigrationsHealthCheck(FinanceTrackerDbContext dbContext)
        {
            _dbContext = dbContext;
        }

        public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
        {
            var pendingMigrations = (await _dbContext.Database.GetPendingMigrationsAsync(cancellationToken)).ToList();

            if (pendingMigrations.Count == 0)
                return HealthCheckResult.Healthy();

            return new HealthCheckResult(
                context.Registration.FailureStatus,
                $"{pendingMigrations.Count} pending migration(s): {string.Join(", ", pendingMigrations)}");
        }
    }
}
