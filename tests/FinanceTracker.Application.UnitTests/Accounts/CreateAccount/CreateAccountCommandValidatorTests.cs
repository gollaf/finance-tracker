using FinanceTracker.Application.Accounts.CreateAccount;
using FinanceTracker.Domain.Accounts;
using FinanceTracker.Domain.Common;
using FluentAssertions;

namespace FinanceTracker.Application.UnitTests.Accounts.CreateAccount
{
    public class CreateAccountCommandValidatorTests
    {
        private static readonly UserId Owner = new(Guid.NewGuid());
        private readonly CreateAccountCommandValidator _validator = new();

        [Fact]
        public void Validate_WithValidCommand_IsValid()
        {
            var result = _validator.Validate(new CreateAccountCommand(Owner, "Checking", AccountType.Checking, "USD"));

            result.IsValid.Should().BeTrue();
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        public void Validate_WithMissingName_IsInvalid(string? name)
        {
            var result = _validator.Validate(new CreateAccountCommand(Owner, name!, AccountType.Checking, "USD"));

            result.IsValid.Should().BeFalse();
            result.Errors.Should().Contain(e => e.PropertyName == nameof(CreateAccountCommand.Name));
        }

        [Fact]
        public void Validate_WithNameLongerThanMax_IsInvalid()
        {
            var name = new string('a', Account.MaxNameLength + 1);

            var result = _validator.Validate(new CreateAccountCommand(Owner, name, AccountType.Checking, "USD"));

            result.IsValid.Should().BeFalse();
            result.Errors.Should().Contain(e => e.PropertyName == nameof(CreateAccountCommand.Name));
        }

        [Theory]
        [InlineData("usd")]
        [InlineData("US")]
        [InlineData("")]
        [InlineData(null)]
        public void Validate_WithInvalidCurrency_IsInvalid(string? currency)
        {
            var result = _validator.Validate(
                new CreateAccountCommand(Owner, "Checking", AccountType.Checking, currency!));

            result.IsValid.Should().BeFalse();
            result.Errors.Should().Contain(e => e.PropertyName == nameof(CreateAccountCommand.Currency));
        }

        [Fact]
        public void Validate_WithUndefinedAccountType_IsInvalid()
        {
            var result = _validator.Validate(new CreateAccountCommand(Owner, "Checking", (AccountType)999, "USD"));

            result.IsValid.Should().BeFalse();
            result.Errors.Should().Contain(e => e.PropertyName == nameof(CreateAccountCommand.Type));
        }

        [Fact]
        public void Validate_WithDefaultOwnerId_IsInvalid()
        {
            var result = _validator.Validate(new CreateAccountCommand(Owner, "Checking", AccountType.Checking, "USD") with { OwnerId = default });

            result.IsValid.Should().BeFalse();
            result.Errors.Should().Contain(e => e.PropertyName == nameof(CreateAccountCommand.OwnerId));
        }
    }
}
