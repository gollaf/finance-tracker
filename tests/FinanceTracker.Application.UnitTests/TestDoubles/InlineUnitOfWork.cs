using FinanceTracker.Application.Common;

namespace FinanceTracker.Application.UnitTests.TestDoubles
{
    /// <summary>
    /// IUnitOfWork for unit tests: runs the operation straight away, with no
    /// real database transaction, and counts how often it was used. Written
    /// by hand rather than with NSubstitute, because a substitute would
    /// never actually call the operation it's handed -- and the operation is
    /// the code under test.
    /// </summary>
    internal sealed class InlineUnitOfWork : IUnitOfWork
    {
        public int Calls { get; private set; }

        public Task<TResult> ExecuteAtomicallyAsync<TResult>(
            Func<CancellationToken, Task<TResult>> operation, CancellationToken cancellationToken = default)
        {
            Calls++;
            return operation(cancellationToken);
        }
    }
}
