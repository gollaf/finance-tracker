namespace FinanceTracker.Application.Common
{
    /// <summary>
    /// Runs several repository calls as ONE all-or-nothing database
    /// transaction. Every repository here saves by itself
    /// (AddAsync/UpdateAsync each call SaveChangesAsync); inside
    /// ExecuteAtomicallyAsync those individual saves are only made permanent
    /// together, when the operation finishes -- and if it throws instead,
    /// every one of them is rolled back. See
    /// docs/adr/0016-asynchronous-csv-import.md.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Only an exception rolls back. An operation that returns normally is
    /// committed, including one that returns a failed Result -- so an
    /// operation that must not leave partial changes behind has to throw.
    /// </para>
    /// <para>
    /// After a rollback, the DbContext's change tracker still believes the
    /// rolled-back entities were saved. The DI scope (one per HTTP request,
    /// one per message) must not keep using it afterwards -- which it
    /// doesn't, since a rollback comes with an exception that ends it.
    /// </para>
    /// </remarks>
    public interface IUnitOfWork
    {
        Task<TResult> ExecuteAtomicallyAsync<TResult>(
            Func<CancellationToken, Task<TResult>> operation, CancellationToken cancellationToken = default);
    }
}
