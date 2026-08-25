using FinanceTracker.Application.Transactions.AddTransaction;
using FinanceTracker.Domain.Common;
using FinanceTracker.Domain.Transactions;
using FluentAssertions;

namespace FinanceTracker.Application.UnitTests.Transactions.AddTransaction
{
    public class AddTransactionCommandValidatorTests
    {
        private readonly AddTransactionCommandValidator _validator = new();
        private static readonly DateOnly Today = DateOnly.FromDateTime(DateTime.UtcNow);

        private static AddTransactionCommand ValidCommand() =>
            new(AccountId.New(), 42.50m, TransactionType.Expense, "Groceries", Today);

        [Fact]
        public void Validate_WithValidCommand_IsValid()
        {
            _validator.Validate(ValidCommand()).IsValid.Should().BeTrue();
        }

        [Fact]
        public void Validate_WithDefaultAccountId_IsInvalid()
        {
            var result = _validator.Validate(ValidCommand() with { AccountId = default });

            result.IsValid.Should().BeFalse();
            result.Errors.Should().Contain(e => e.PropertyName == nameof(AddTransactionCommand.AccountId));
        }

        [Theory]
        [InlineData(0)]
        [InlineData(-5)]
        public void Validate_WithNonPositiveAmount_IsInvalid(decimal amount)
        {
            var result = _validator.Validate(ValidCommand() with { Amount = amount });

            result.IsValid.Should().BeFalse();
            result.Errors.Should().Contain(e => e.PropertyName == nameof(AddTransactionCommand.Amount));
        }

        [Fact]
        public void Validate_WithUndefinedTransactionType_IsInvalid()
        {
            var result = _validator.Validate(ValidCommand() with { Type = (TransactionType)999 });

            result.IsValid.Should().BeFalse();
            result.Errors.Should().Contain(e => e.PropertyName == nameof(AddTransactionCommand.Type));
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        public void Validate_WithMissingDescription_IsInvalid(string? description)
        {
            var result = _validator.Validate(ValidCommand() with { Description = description! });

            result.IsValid.Should().BeFalse();
            result.Errors.Should().Contain(e => e.PropertyName == nameof(AddTransactionCommand.Description));
        }

        [Fact]
        public void Validate_WithDescriptionLongerThanMax_IsInvalid()
        {
            var tooLong = new string('a', Transaction.MaxDescriptionLength + 1);

            var result = _validator.Validate(ValidCommand() with { Description = tooLong });

            result.IsValid.Should().BeFalse();
            result.Errors.Should().Contain(e => e.PropertyName == nameof(AddTransactionCommand.Description));
        }

        [Fact]
        public void Validate_WithFutureDate_IsInvalid()
        {
            var result = _validator.Validate(ValidCommand() with { OccurredOn = Today.AddDays(1) });

            result.IsValid.Should().BeFalse();
            result.Errors.Should().Contain(e => e.PropertyName == nameof(AddTransactionCommand.OccurredOn));
        }
    }
}
