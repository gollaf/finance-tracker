using FinanceTracker.Application.Transactions.UpdateTransaction;
using FinanceTracker.Domain.Common;
using FinanceTracker.Domain.Transactions;
using FluentAssertions;

namespace FinanceTracker.Application.UnitTests.Transactions.UpdateTransaction
{
    public class UpdateTransactionCommandValidatorTests
    {
        private readonly UpdateTransactionCommandValidator _validator = new();
        private static readonly DateOnly Today = DateOnly.FromDateTime(DateTime.UtcNow);

        private static UpdateTransactionCommand ValidCommand() =>
            new(TransactionId.New(), 35m, "New description", Today);

        [Fact]
        public void Validate_WithValidCommand_IsValid()
        {
            _validator.Validate(ValidCommand()).IsValid.Should().BeTrue();
        }

        [Fact]
        public void Validate_WithDefaultTransactionId_IsInvalid()
        {
            var result = _validator.Validate(ValidCommand() with { TransactionId = default });

            result.IsValid.Should().BeFalse();
            result.Errors.Should().Contain(e => e.PropertyName == nameof(UpdateTransactionCommand.TransactionId));
        }

        [Theory]
        [InlineData(0)]
        [InlineData(-5)]
        public void Validate_WithNonPositiveAmount_IsInvalid(decimal amount)
        {
            var result = _validator.Validate(ValidCommand() with { Amount = amount });

            result.IsValid.Should().BeFalse();
            result.Errors.Should().Contain(e => e.PropertyName == nameof(UpdateTransactionCommand.Amount));
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        public void Validate_WithMissingDescription_IsInvalid(string? description)
        {
            var result = _validator.Validate(ValidCommand() with { Description = description! });

            result.IsValid.Should().BeFalse();
            result.Errors.Should().Contain(e => e.PropertyName == nameof(UpdateTransactionCommand.Description));
        }

        [Fact]
        public void Validate_WithDescriptionLongerThanMax_IsInvalid()
        {
            var tooLong = new string('a', Transaction.MaxDescriptionLength + 1);

            var result = _validator.Validate(ValidCommand() with { Description = tooLong });

            result.IsValid.Should().BeFalse();
            result.Errors.Should().Contain(e => e.PropertyName == nameof(UpdateTransactionCommand.Description));
        }

        [Fact]
        public void Validate_WithFutureDate_IsInvalid()
        {
            var result = _validator.Validate(ValidCommand() with { OccurredOn = Today.AddDays(1) });

            result.IsValid.Should().BeFalse();
            result.Errors.Should().Contain(e => e.PropertyName == nameof(UpdateTransactionCommand.OccurredOn));
        }
    }
}
