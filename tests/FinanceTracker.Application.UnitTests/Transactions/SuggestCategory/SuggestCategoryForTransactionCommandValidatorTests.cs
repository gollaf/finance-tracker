using FinanceTracker.Application.Transactions.SuggestCategory;
using FinanceTracker.Domain.Common;
using FluentAssertions;

namespace FinanceTracker.Application.UnitTests.Transactions.SuggestCategory
{
    public class SuggestCategoryForTransactionCommandValidatorTests
    {
        private static readonly UserId Owner = new(Guid.NewGuid());
        private readonly SuggestCategoryForTransactionCommandValidator _validator = new();

        [Fact]
        public void Validate_WithTransactionId_IsValid()
        {
            var command = new SuggestCategoryForTransactionCommand(Owner, TransactionId.New());

            _validator.Validate(command).IsValid.Should().BeTrue();
        }

        [Fact]
        public void Validate_WithDefaultTransactionId_IsInvalid()
        {
            var command = new SuggestCategoryForTransactionCommand(Owner, default);

            var result = _validator.Validate(command);

            result.IsValid.Should().BeFalse();
            result.Errors.Should().Contain(e => e.PropertyName == nameof(SuggestCategoryForTransactionCommand.TransactionId));
        }

        [Fact]
        public void Validate_WithDefaultOwnerId_IsInvalid()
        {
            var result = _validator.Validate(new SuggestCategoryForTransactionCommand(Owner, TransactionId.New()) with { OwnerId = default });

            result.IsValid.Should().BeFalse();
            result.Errors.Should().Contain(e => e.PropertyName == nameof(SuggestCategoryForTransactionCommand.OwnerId));
        }
    }
}
