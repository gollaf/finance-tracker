using FinanceTracker.Application.Common;
using FluentAssertions;

namespace FinanceTracker.Application.UnitTests.Common
{
    public class ResultTests
    {
        [Fact]
        public void Success_HasNoError()
        {
            var result = Result.Success();

            result.IsSuccess.Should().BeTrue();
            result.IsFailure.Should().BeFalse();
            result.Error.Should().Be(Error.None);
        }

        [Fact]
        public void Failure_CarriesTheGivenError()
        {
            var error = Error.Validation("Test.Invalid", "was invalid");

            var result = Result.Failure(error);

            result.IsSuccess.Should().BeFalse();
            result.IsFailure.Should().BeTrue();
            result.Error.Should().Be(error);
        }

        [Fact]
        public void GenericSuccess_ExposesValue()
        {
            var result = Result.Success(42);

            result.IsSuccess.Should().BeTrue();
            result.Value.Should().Be(42);
        }

        [Fact]
        public void GenericFailure_AccessingValue_Throws()
        {
            var result = Result.Failure<int>(Error.NotFound("Test.NotFound", "missing"));

            var act = () => result.Value;

            act.Should().Throw<InvalidOperationException>();
        }

        [Fact]
        public void GenericFailure_CarriesTheGivenError()
        {
            var error = Error.Conflict("Test.Conflict", "already exists");

            var result = Result.Failure<int>(error);

            result.IsFailure.Should().BeTrue();
            result.Error.Should().Be(error);
        }

        [Fact]
        public void ImplicitConversion_FromValue_ProducesSuccess()
        {
            Result<int> result = 42;

            result.IsSuccess.Should().BeTrue();
            result.Value.Should().Be(42);
        }
    }
}
