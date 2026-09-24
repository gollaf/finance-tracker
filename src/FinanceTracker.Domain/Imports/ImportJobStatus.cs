namespace FinanceTracker.Domain.Imports
{
    public enum ImportJobStatus
    {
        /// <summary>Accepted and waiting to be processed.</summary>
        Pending,

        /// <summary>
        /// Processed. Rows that failed are listed in ImportJob.Errors -- a
        /// Completed job can still have skipped some rows.
        /// </summary>
        Completed,

        /// <summary>
        /// Could not be processed at all (for example, its Account was
        /// closed in the meantime). Nothing was imported.
        /// </summary>
        Failed,
    }
}
