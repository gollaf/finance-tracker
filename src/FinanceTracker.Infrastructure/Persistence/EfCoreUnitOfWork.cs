using FinanceTracker.Application.Common;

namespace FinanceTracker.Infrastructure.Persistence
{
    /// <summary>
    /// IUnitOfWork over an explicit database transaction on the scope's one
    /// shared FinanceTrackerDbContext. Every SaveChangesAsync a repository
    /// makes while it is open runs inside that transaction instead of
    /// committing on its own, so nothing is permanent until CommitAsync.
    /// </summary>
    public sealed class EfCoreUnitOfWork : IUnitOfWork
    {
        private readonly FinanceTrackerDbContext _dbContext;

        public EfCoreUnitOfWork(FinanceTrackerDbContext dbContext)
        {
            _dbContext = dbContext;
        }

        public async Task<TResult> ExecuteAtomicallyAsync<TResult>(
            Func<CancellationToken, Task<TResult>> operation, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(operation);

            // Already inside a transaction: join it rather than start a
            // second one, which would fail.
            if (_dbContext.Database.CurrentTransaction is not null)
                return await operation(cancellationToken);

            await using var transaction = await _dbContext.Database.BeginTransactionAsync(cancellationToken);

            // If operation throws, disposing the uncommitted transaction rolls it back.
            var result = await operation(cancellationToken);
            await transaction.CommitAsync(cancellationToken);

            return result;
        }
    }
}
