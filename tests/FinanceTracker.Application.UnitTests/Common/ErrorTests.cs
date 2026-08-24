using FinanceTracker.Application.Common;
using FluentAssertions;

namespace FinanceTracker.Application.UnitTests.Common
{
    public class ErrorTests
    {
        [Fact]
        public void None_HasNoneType()
        {
            Error.None.Type.Should().Be(ErrorType.None);
        }

        [Fact]
        public void Validation_SetsExpectedTypeAndValues()
        {
            var error = Error.Validation("code", "message");

            error.Type.Should().Be(ErrorType.Validation);
            error.Code.Should().Be("code");
            error.Message.Should().Be("message");
        }

        [Fact]
        public void NotFound_SetsExpectedType()
        {
            Error.NotFound("code", "message").Type.Should().Be(ErrorType.NotFound);
        }

        [Fact]
        public void Conflict_SetsExpectedType()
        {
            Error.Conflict("code", "message").Type.Should().Be(ErrorType.Conflict);
        }

        [Fact]
        public void Failure_SetsExpectedType()
        {
            Error.Failure("code", "message").Type.Should().Be(ErrorType.Failure);
        }

        [Fact]
        public void TwoErrors_WithSameValues_AreEqual()
        {
            Error.NotFound("a", "b").Should().Be(Error.NotFound("a", "b"));
        }
    }
}
