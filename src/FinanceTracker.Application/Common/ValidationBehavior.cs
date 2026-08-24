using FluentValidation;
using MediatR;

namespace FinanceTracker.Application.Common
{
    /// <summary>
    /// Runs every registered FluentValidation validator for a request before
    /// its handler executes. On failure, short-circuits the pipeline and
    /// returns a failed Result (or Result&lt;TValue&gt;) instead of calling
    /// the handler — handlers never validate their own input.
    /// </summary>
    public sealed class ValidationBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
        where TRequest : notnull
        where TResponse : Result
    {
        private readonly IEnumerable<IValidator<TRequest>> _validators;

        public ValidationBehavior(IEnumerable<IValidator<TRequest>> validators)
        {
            _validators = validators;
        }

        public async Task<TResponse> Handle(
            TRequest request,
            RequestHandlerDelegate<TResponse> next,
            CancellationToken cancellationToken)
        {
            if (!_validators.Any())
                return await next();

            var failures = new List<string>();

            foreach (var validator in _validators)
            {
                var result = await validator.ValidateAsync(request, cancellationToken);
                failures.AddRange(result.Errors.Select(f => f.ErrorMessage));
            }

            if (failures.Count == 0)
                return await next();

            var error = Error.Validation("Validation.Failed", string.Join(" | ", failures));

            return BuildFailureResponse(error);
        }

        private static TResponse BuildFailureResponse(Error error)
        {
            if (typeof(TResponse) == typeof(Result))
                return (TResponse)(object)Result.Failure(error);

            var valueType = typeof(TResponse).GetGenericArguments()[0];
            var failureMethod = typeof(Result)
                .GetMethods()
                .Single(m => m.Name == nameof(Result.Failure) && m.IsGenericMethod)
                .MakeGenericMethod(valueType);

            return (TResponse)failureMethod.Invoke(null, new object[] { error })!;
        }
    }
}
