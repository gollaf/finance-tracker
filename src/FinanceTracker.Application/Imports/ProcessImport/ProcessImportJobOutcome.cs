namespace FinanceTracker.Application.Imports.ProcessImport
{
    public enum ProcessImportJobOutcome
    {
        /// <summary>The rows were imported; any that failed are listed on the job.</summary>
        Completed,

        /// <summary>The job couldn't be processed (its Account is gone or closed) and was marked Failed.</summary>
        Failed,

        /// <summary>
        /// The job was no longer Pending: an earlier delivery of the same
        /// message already processed it. Nothing was done -- this is what
        /// makes a duplicate delivery harmless.
        /// </summary>
        AlreadyProcessed,

        /// <summary>No job with this id exists.</summary>
        JobNotFound,
    }
}
