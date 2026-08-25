namespace FinanceTracker.Application.Common
{
    /// <summary>
    /// Outcome of a use case that doesn't produce a value. Handlers return
    /// this (or Result&lt;TValue&gt;) instead of throwing, so a failure —
    /// not found, validation, conflict — is an ordinary value the caller
    /// has to look at, not an exception a later layer has to translate.
    /// </summary>
    public class Result
    {
        public bool IsSuccess { get; }

        public bool IsFailure => !IsSuccess;

        public Error Error { get; }

        protected Result(bool isSuccess, Error error)
        {
            IsSuccess = isSuccess;
            Error = error;
        }

        public static Result Success() => new(true, Error.None);

        public static Result Failure(Error error) => new(false, error);

        public static Result<TValue> Success<TValue>(TValue value) => new(value, true, Error.None);

        public static Result<TValue> Failure<TValue>(Error error) => new(default, false, error);
    }

    /// <summary>Outcome of a use case that produces a value on success.</summary>
    public class Result<TValue> : Result
    {
        private readonly TValue? _value;

        protected internal Result(TValue? value, bool isSuccess, Error error)
            : base(isSuccess, error)
        {
            _value = value;
        }

        /// <summary>The success value. Throws if this Result is a failure — check IsSuccess first.</summary>
        public TValue Value => IsSuccess
            ? _value!
            : throw new InvalidOperationException("Cannot access Value on a failed Result.");
    }
}
