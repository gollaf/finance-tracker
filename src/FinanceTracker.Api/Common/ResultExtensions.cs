using FinanceTracker.Application.Common;
using Microsoft.AspNetCore.Mvc;

namespace FinanceTracker.Api.Common
{
    /// <summary>
    /// The one place a failed Result becomes an HTTP response, per ADR
    /// 0004's fixed ErrorType-to-status table. Only the failure path is
    /// generic here on purpose -- success shape (201 vs 200 vs 204, what
    /// goes in the body, any Location header) is decided per action, since
    /// it genuinely differs per use case in a way failures don't.
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
                // ErrorType.Failure, and anything unrecognized, both fall
                // back to 500 with a generic detail -- per ADR 0004, a
                // Failure's own Error.Message is not shown to the client.
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
