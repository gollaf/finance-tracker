using FinanceTracker.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace FinanceTracker.Api.HealthChecks
{
    /// <summary>
    /// Readiness check: healthy only once every migration this build knows
    /// about has been applied to the database. When migrations run as a
    /// separate step instead of inside this process's own startup (the
    /// `migrate` command, ADR 0017), an instance can otherwise start, find
    /// the database reachable, report ready -- and then fail every real
    /// request because its tables don't exist yet.
    ///
    /// Only this build's own migrations are compared against the database,
    /// so an older instance stays ready after a newer version has migrated
    /// further ahead: what's applied is then a superset of what it knows
    /// about. That is only safe if each migration stays compatible with the
    /// previous version of the code -- a rule for writing migrations, not
    /// something this check can enforce.
    ///
    /// Not registered in DI: the health check service creates a fresh
    /// instance, inside a fresh DI scope, on every run, so the scoped
    /// DbContext injected here is never shared with a request. If the
    /// database can't be reached, the exception thrown here is caught by
    /// that service and reported as this check's failure status.
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
