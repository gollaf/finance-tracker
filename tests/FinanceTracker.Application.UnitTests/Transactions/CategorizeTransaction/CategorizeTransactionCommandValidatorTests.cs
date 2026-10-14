using FinanceTracker.Application.Transactions.CategorizeTransaction;
using FinanceTracker.Domain.Common;
using FluentAssertions;

namespace FinanceTracker.Application.UnitTests.Transactions.CategorizeTransaction
{
    public class CategorizeTransactionCommandValidatorTests
    {
        private static readonly UserId Owner = new(Guid.NewGuid());
        private readonly CategorizeTransactionCommandValidator _validator = new();

        [Fact]
        public void Validate_WithValidCategoryId_IsValid()
        {
            var command = new CategorizeTransactionCommand(Owner, TransactionId.New(), CategoryId.New());

            _validator.Validate(command).IsValid.Should().BeTrue();
        }

        [Fact]
        public void Validate_WithNullCategoryId_IsValid()
        {
            var command = new CategorizeTransactionCommand(Owner, TransactionId.New(), null);

            _validator.Validate(command).IsValid.Should().BeTrue();
        }

        [Fact]
        public void Validate_WithDefaultTransactionId_IsInvalid()
        {
            var command = new CategorizeTransactionCommand(Owner, default, CategoryId.New());

            var result = _validator.Validate(command);

            result.IsValid.Should().BeFalse();
            result.Errors.Should().Contain(e => e.PropertyName == nameof(CategorizeTransactionCommand.TransactionId));
        }

        [Fact]
        public void Validate_WithDefaultCategoryId_IsInvalid()
        {
            var command = new CategorizeTransactionCommand(Owner, TransactionId.New(), default(CategoryId));

            var result = _validator.Validate(command);

            result.IsValid.Should().BeFalse();
            result.Errors.Should().Contain(e => e.PropertyName == nameof(CategorizeTransactionCommand.CategoryId));
        }

        [Fact]
        public void Validate_WithDefaultOwnerId_IsInvalid()
        {
            var result = _validator.Validate(new CategorizeTransactionCommand(Owner, TransactionId.New(), CategoryId.New()) with { OwnerId = default });

            result.IsValid.Should().BeFalse();
            result.Errors.Should().Contain(e => e.PropertyName == nameof(CategorizeTransactionCommand.OwnerId));
        }
    }
}
