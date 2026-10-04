namespace FinanceTracker.Application.Common
{
    /// <summary>What kind of failure an Error is; the Api maps each one to an HTTP status (e.g. NotFound -> 404).</summary>
    public enum ErrorType
    {
        None,
        Validation,
        NotFound,
        Conflict,
        Failure
    }
}
