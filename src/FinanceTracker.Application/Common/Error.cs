namespace FinanceTracker.Application.Common
{
    /// <summary>A use-case failure: a stable Code, a human-readable Message, and a Type.</summary>
    public sealed record Error(string Code, string Message, ErrorType Type)
    {
        public static readonly Error None = new(string.Empty, string.Empty, ErrorType.None);

        public static Error Validation(string code, string message) => new(code, message, ErrorType.Validation);

        public static Error NotFound(string code, string message) => new(code, message, ErrorType.NotFound);

        public static Error Conflict(string code, string message) => new(code, message, ErrorType.Conflict);

        public static Error Failure(string code, string message) => new(code, message, ErrorType.Failure);
    }
}
