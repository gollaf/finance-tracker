using FinanceTracker.Domain.Accounts;
using FluentAssertions;

namespace FinanceTracker.Domain.UnitTests.Accounts
{
    public class AccountTests
    {
        [Fact]
        public void Create_WithValidData_Succeeds()
        {
            var account = Account.Create("Checking", AccountType.Checking, "USD");

            account.Name.Should().Be("Checking");
            account.Type.Should().Be(AccountType.Checking);
            account.Currency.Should().Be("USD");
            account.IsClosed.Should().BeFalse();
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        public void Create_WithMissingName_Throws(string? name)
        {
            var act = () => Account.Create(name!, AccountType.Checking, "USD");

            act.Should().Throw<ArgumentException>();
        }

        [Fact]
        public void Create_WithNameLongerThanMax_Throws()
        {
            var name = new string('a', Account.MaxNameLength + 1);

            var act = () => Account.Create(name, AccountType.Checking, "USD");

            act.Should().Throw<ArgumentException>();
        }

        [Theory]
        [InlineData("usd")]
        [InlineData("US")]
        [InlineData("")]
        public void Create_WithInvalidCurrency_Throws(string currency)
        {
            var act = () => Account.Create("Checking", AccountType.Checking, currency);

            act.Should().Throw<ArgumentException>();
        }

        [Fact]
        public void Rename_WithValidName_UpdatesName()
        {
            var account = Account.Create("Checking", AccountType.Checking, "USD");

            account.Rename("Everyday Checking");

            account.Name.Should().Be("Everyday Checking");
        }

        [Fact]
        public void Rename_WhenClosed_Throws()
        {
            var account = Account.Create("Checking", AccountType.Checking, "USD");
            account.Close();

            var act = () => account.Rename("New Name");

            act.Should().Throw<InvalidOperationException>();
        }

        [Fact]
        public void Close_MarksAccountClosed()
        {
            var account = Account.Create("Checking", AccountType.Checking, "USD");

            account.Close();

            account.IsClosed.Should().BeTrue();
        }

        [Fact]
        public void Close_WhenAlreadyClosed_Throws()
        {
            var account = Account.Create("Checking", AccountType.Checking, "USD");
            account.Close();

            var act = () => account.Close();

            act.Should().Throw<InvalidOperationException>();
        }
    }
}
