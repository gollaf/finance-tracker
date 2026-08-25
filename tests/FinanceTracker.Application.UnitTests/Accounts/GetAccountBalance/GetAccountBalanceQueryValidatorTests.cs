using FinanceTracker.Application.Accounts.GetAccountBalance;
using FinanceTracker.Domain.Common;
using FluentAssertions;

namespace FinanceTracker.Application.UnitTests.Accounts.GetAccountBalance
{
    public class GetAccountBalanceQueryValidatorTests
    {
        private readonly GetAccountBalanceQueryValidator _validator = new();

        [Fact]
        public void Validate_WithValidQuery_IsValid()
        {
            var query = new GetAccountBalanceQuery(AccountId.New());

            _validator.Validate(query).IsValid.Should().BeTrue();
        }

        [Fact]
        public void Validate_WithDefaultAccountId_IsInvalid()
        {
            var query = new GetAccountBalanceQuery(default);

            var result = _validator.Validate(query);

            result.IsValid.Should().BeFalse();
            result.Errors.Should().Contain(e => e.PropertyName == nameof(GetAccountBalanceQuery.AccountId));
        }
    }
}
