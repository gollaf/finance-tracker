using FinanceTracker.Domain.Common;
using FluentAssertions;

namespace FinanceTracker.Domain.UnitTests.Common
{
    public class MoneyTests
    {
        [Fact]
        public void Create_WithValidAmountAndCurrency_Succeeds()
        {
            var money = Money.Create(10.50m, "USD");

            money.Amount.Should().Be(10.50m);
            money.Currency.Should().Be("USD");
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        public void Create_WithMissingCurrency_Throws(string? currency)
        {
            var act = () => Money.Create(10m, currency!);

            act.Should().Throw<ArgumentException>();
        }

        [Theory]
        [InlineData("usd")]
        [InlineData("US")]
        [InlineData("USDD")]
        [InlineData("12A")]
        public void Create_WithInvalidCurrencyFormat_Throws(string currency)
        {
            var act = () => Money.Create(10m, currency);

            act.Should().Throw<ArgumentException>();
        }

        [Fact]
        public void TwoMoneyInstances_WithSameAmountAndCurrency_AreEqual()
        {
            var a = Money.Create(5m, "USD");
            var b = Money.Create(5m, "USD");

            a.Should().Be(b);
        }

        [Fact]
        public void Add_SameCurrency_ReturnsSum()
        {
            var a = Money.Create(5m, "USD");
            var b = Money.Create(2.5m, "USD");

            (a + b).Should().Be(Money.Create(7.5m, "USD"));
        }

        [Fact]
        public void Add_DifferentCurrency_Throws()
        {
            var a = Money.Create(5m, "USD");
            var b = Money.Create(2m, "EUR");

            var act = () => a + b;

            act.Should().Throw<InvalidOperationException>();
        }

        [Fact]
        public void Subtract_SameCurrency_ReturnsDifference()
        {
            var a = Money.Create(5m, "USD");
            var b = Money.Create(2m, "USD");

            (a - b).Should().Be(Money.Create(3m, "USD"));
        }

        [Fact]
        public void Subtract_DifferentCurrency_Throws()
        {
            var a = Money.Create(5m, "USD");
            var b = Money.Create(2m, "EUR");

            var act = () => a - b;

            act.Should().Throw<InvalidOperationException>();
        }

        [Fact]
        public void Negate_ReturnsOppositeSignAmount()
        {
            var money = Money.Create(5m, "USD");

            money.Negate().Should().Be(Money.Create(-5m, "USD"));
        }

        [Theory]
        [InlineData(0, true, false, false)]
        [InlineData(5, false, true, false)]
        [InlineData(-5, false, false, true)]
        public void SignHelpers_ReflectAmount(decimal amount, bool isZero, bool isPositive, bool isNegative)
        {
            var money = Money.Create(amount, "USD");

            money.IsZero.Should().Be(isZero);
            money.IsPositive.Should().Be(isPositive);
            money.IsNegative.Should().Be(isNegative);
        }
    }
}
