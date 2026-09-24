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

            // Already inside a transaction (an atomic operation calling
            // another one): just take part in the outer one, which decides
            // whether everything commits. Starting a second transaction on
            // the same connection is an error.
            if (_dbContext.Database.CurrentTransaction is not null)
                return await operation(cancellationToken);

            await using var transaction = await _dbContext.Database.BeginTransactionAsync(cancellationToken);

            // If operation throws, CommitAsync is never reached, and
            // disposing a transaction that was never committed rolls it
            // back -- that is the whole rollback path.
            var result = await operation(cancellationToken);
            await transaction.CommitAsync(cancellationToken);

            return result;
        }
    }
}
