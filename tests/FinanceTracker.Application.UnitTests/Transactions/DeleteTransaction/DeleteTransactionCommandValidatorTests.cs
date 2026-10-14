using FinanceTracker.Application.Transactions.DeleteTransaction;
using FinanceTracker.Domain.Common;
using FluentAssertions;

namespace FinanceTracker.Application.UnitTests.Transactions.DeleteTransaction
{
    public class DeleteTransactionCommandValidatorTests
    {
        private static readonly UserId Owner = new(Guid.NewGuid());
        private readonly DeleteTransactionCommandValidator _validator = new();

        [Fact]
        public void Validate_WithValidCommand_IsValid()
        {
            var command = new DeleteTransactionCommand(Owner, TransactionId.New());

            _validator.Validate(command).IsValid.Should().BeTrue();
        }

        [Fact]
        public void Validate_WithDefaultTransactionId_IsInvalid()
        {
            var command = new DeleteTransactionCommand(Owner, default);

            var result = _validator.Validate(command);

            result.IsValid.Should().BeFalse();
            result.Errors.Should().Contain(e => e.PropertyName == nameof(DeleteTransactionCommand.TransactionId));
        }

        [Fact]
        public void Validate_WithDefaultOwnerId_IsInvalid()
        {
            var result = _validator.Validate(new DeleteTransactionCommand(Owner, TransactionId.New()) with { OwnerId = default });

            result.IsValid.Should().BeFalse();
            result.Errors.Should().Contain(e => e.PropertyName == nameof(DeleteTransactionCommand.OwnerId));
        }
    }
}
