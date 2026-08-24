using FinanceTracker.Application.Common;
using FluentAssertions;
using FluentValidation;
using MediatR;

namespace FinanceTracker.Application.UnitTests.Common
{
    public class ValidationBehaviorTests
    {
        private sealed record TestCommand(string Name) : IRequest<Result>;

        private sealed class TestCommandValidator : AbstractValidator<TestCommand>
        {
            public TestCommandValidator()
            {
                RuleFor(c => c.Name).NotEmpty();
            }
        }

        // A second, independent rule set — used to prove the pipeline
        // aggregates failures across multiple validators for one request,
        // not just the first one that fails.
        private sealed class SecondTestCommandValidator : AbstractValidator<TestCommand>
        {
            public SecondTestCommandValidator()
            {
                RuleFor(c => c.Name).Must(_ => false).WithMessage("Second failure");
            }
        }

        private sealed record TestQuery(string Name) : IRequest<Result<Guid>>;

        private sealed class TestQueryValidator : AbstractValidator<TestQuery>
        {
            public TestQueryValidator()
            {
                RuleFor(q => q.Name).NotEmpty();
            }
        }

        [Fact]
        public async Task Handle_WithNoValidators_CallsNext()
        {
            var behavior = new ValidationBehavior<TestCommand, Result>(Array.Empty<IValidator<TestCommand>>());
            var nextCalled = false;

            var response = await behavior.Handle(
                new TestCommand("ok"),
                () => { nextCalled = true; return Task.FromResult(Result.Success()); },
                CancellationToken.None);

            nextCalled.Should().BeTrue();
            response.IsSuccess.Should().BeTrue();
        }

        [Fact]
        public async Task Handle_WithValidRequest_CallsNext()
        {
            var behavior = new ValidationBehavior<TestCommand, Result>(new[] { new TestCommandValidator() });
            var nextCalled = false;

            var response = await behavior.Handle(
                new TestCommand("ok"),
                () => { nextCalled = true; return Task.FromResult(Result.Success()); },
                CancellationToken.None);

            nextCalled.Should().BeTrue();
            response.IsSuccess.Should().BeTrue();
        }

        [Fact]
        public async Task Handle_WithInvalidRequest_ReturnsFailureWithoutCallingNext()
        {
            var behavior = new ValidationBehavior<TestCommand, Result>(new[] { new TestCommandValidator() });
            var nextCalled = false;

            var response = await behavior.Handle(
                new TestCommand(""),
                () => { nextCalled = true; return Task.FromResult(Result.Success()); },
                CancellationToken.None);

            nextCalled.Should().BeFalse();
            response.IsFailure.Should().BeTrue();
            response.Error.Type.Should().Be(ErrorType.Validation);
        }

        [Fact]
        public async Task Handle_WithInvalidRequest_AndGenericResultResponse_ReturnsTypedFailure()
        {
            var behavior = new ValidationBehavior<TestQuery, Result<Guid>>(new[] { new TestQueryValidator() });

            var response = await behavior.Handle(
                new TestQuery(""),
                () => Task.FromResult(Result.Success(Guid.NewGuid())),
                CancellationToken.None);

            response.IsFailure.Should().BeTrue();
            response.Error.Type.Should().Be(ErrorType.Validation);
        }

        [Fact]
        public async Task Handle_WithMultipleValidators_AggregatesFailures()
        {
            var behavior = new ValidationBehavior<TestCommand, Result>(
                new IValidator<TestCommand>[] { new TestCommandValidator(), new SecondTestCommandValidator() });

            var response = await behavior.Handle(
                new TestCommand(""),
                () => Task.FromResult(Result.Success()),
                CancellationToken.None);

            response.Error.Message.Should().Contain("Second failure");
        }
    }
}
