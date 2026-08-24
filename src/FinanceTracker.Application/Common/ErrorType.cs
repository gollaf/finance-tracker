namespace FinanceTracker.Application.Common
{
    /// <summary>How the Api layer should map a failed Result once it exists (e.g. NotFound -> 404).</summary>
    public enum ErrorType
    {
        None,
        Validation,
        NotFound,
        Conflict,
        Failure
    }
}
