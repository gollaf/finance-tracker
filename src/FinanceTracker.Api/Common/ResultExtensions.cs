using FinanceTracker.Application.Common;
using Microsoft.AspNetCore.Mvc;

namespace FinanceTracker.Api.Common
{
    /// <summary>
    /// The one place a failed Result becomes an HTTP response (ADR 0004).
    /// Only failures are mapped here; each action shapes its own success
    /// response.
    /// </summary>
    public static class ResultExtensions
    {
        public static IActionResult ToActionResult(this Result result)
        {
            if (result.IsSuccess)
            {
                throw new InvalidOperationException(
                    "ToActionResult() is for the failure path only -- handle IsSuccess explicitly in the action first.");
            }

            return result.Error.Type switch
            {
                ErrorType.Validation => Problem(result.Error, StatusCodes.Status400BadRequest),
                ErrorType.NotFound => Problem(result.Error, StatusCodes.Status404NotFound),
                ErrorType.Conflict => Problem(result.Error, StatusCodes.Status409Conflict),
                // Failure and anything unknown: 500, without the internal message.
                _ => Problem(result.Error, StatusCodes.Status500InternalServerError, genericDetail: true),
            };
        }

        private static ObjectResult Problem(Error error, int statusCode, bool genericDetail = false)
        {
            var problemDetails = new ProblemDetails
            {
                Status = statusCode,
                Title = error.Code,
                Detail = genericDetail ? "An unexpected error occurred." : error.Message,
            };

            return new ObjectResult(problemDetails) { StatusCode = statusCode };
        }
    }
}
