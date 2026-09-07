using FinanceTracker.Api.Common;
using FinanceTracker.Application.Common;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace FinanceTracker.Api.UnitTests.Common
{
    /// <summary>
    /// Exhaustively checks ADR 0004's ErrorType-to-HTTP-status table so
    /// that table has exactly one place it can drift from what's actually
    /// implemented: this test file.
    /// </summary>
    public sealed class ResultExtensionsTests
    {
        [Fact]
        public void ToActionResult_WithValidationError_Returns400WithMessageAsDetail()
        {
            var result = Result.Failure(Error.Validation("Some.Code", "Some message."));

            var actionResult = result.ToActionResult();

            var problem = AssertProblemDetails(actionResult, StatusCodes.Status400BadRequest);
            problem.Title.Should().Be("Some.Code");
            problem.Detail.Should().Be("Some message.");
        }

        [Fact]
        public void ToActionResult_WithNotFoundError_Returns404WithMessageAsDetail()
        {
            var result = Result.Failure(Error.NotFound("Some.Code", "Some message."));

            var actionResult = result.ToActionResult();

            var problem = AssertProblemDetails(actionResult, StatusCodes.Status404NotFound);
            problem.Detail.Should().Be("Some message.");
        }

        [Fact]
        public void ToActionResult_WithConflictError_Returns409WithMessageAsDetail()
        {
            var result = Result.Failure(Error.Conflict("Some.Code", "Some message."));

            var actionResult = result.ToActionResult();

            var problem = AssertProblemDetails(actionResult, StatusCodes.Status409Conflict);
            problem.Detail.Should().Be("Some message.");
        }

        [Fact]
        public void ToActionResult_WithFailureError_Returns500WithGenericDetail_NotTheRawMessage()
        {
            var result = Result.Failure(Error.Failure("Some.Code", "Some internal detail that must not leak."));

            var actionResult = result.ToActionResult();

            var problem = AssertProblemDetails(actionResult, StatusCodes.Status500InternalServerError);
            problem.Detail.Should().Be("An unexpected error occurred.");
            problem.Detail.Should().NotContain("internal detail");
        }

        [Fact]
        public void ToActionResult_WithGenericResultOfT_AlsoMapsCorrectly()
        {
            // Result<TValue> derives from Result, so the same extension
            // method has to work when called through the generic type too
            // -- this is what every real controller action actually does.
            Result<Guid> result = Result.Failure<Guid>(Error.NotFound("Account.NotFound", "No such account."));

            var actionResult = result.ToActionResult();

            AssertProblemDetails(actionResult, StatusCodes.Status404NotFound);
        }

        [Fact]
        public void ToActionResult_WithSuccessResult_Throws()
        {
            var result = Result.Success();

            var act = () => result.ToActionResult();

            act.Should().Throw<InvalidOperationException>();
        }

        private static ProblemDetails AssertProblemDetails(IActionResult actionResult, int expectedStatusCode)
        {
            var objectResult = actionResult.Should().BeOfType<ObjectResult>().Subject;
            objectResult.StatusCode.Should().Be(expectedStatusCode);

            var problem = objectResult.Value.Should().BeOfType<ProblemDetails>().Subject;
            problem.Status.Should().Be(expectedStatusCode);

            return problem;
        }
    }
}
