using FinanceTracker.Application.Transactions.GetTransactions;
using FinanceTracker.Domain.Common;
using FluentAssertions;

namespace FinanceTracker.Application.UnitTests.Transactions.GetTransactions
{
    public class GetTransactionsQueryValidatorTests
    {
        private readonly GetTransactionsQueryValidator _validator = new();

        [Fact]
        public void Validate_WithValidQuery_IsValid()
        {
            var query = new GetTransactionsQuery(AccountId.New());

            _validator.Validate(query).IsValid.Should().BeTrue();
        }

        [Fact]
        public void Validate_WithDefaultAccountId_IsInvalid()
        {
            var query = new GetTransactionsQuery(default);

            var result = _validator.Validate(query);

            result.IsValid.Should().BeFalse();
            result.Errors.Should().Contain(e => e.PropertyName == nameof(GetTransactionsQuery.AccountId));
        }

        [Fact]
        public void Validate_WithFromAfterTo_IsInvalid()
        {
            var query = new GetTransactionsQuery(AccountId.New(), new DateOnly(2026, 6, 1), new DateOnly(2026, 1, 1));

            var result = _validator.Validate(query);

            result.IsValid.Should().BeFalse();
            result.Errors.Should().Contain(e => e.PropertyName == nameof(GetTransactionsQuery.To));
        }

        [Fact]
        public void Validate_WithOnlyFromProvided_IsValid()
        {
            var query = new GetTransactionsQuery(AccountId.New(), new DateOnly(2026, 1, 1));

            _validator.Validate(query).IsValid.Should().BeTrue();
        }
    }
}
