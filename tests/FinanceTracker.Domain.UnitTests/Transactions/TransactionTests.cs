using FinanceTracker.Domain.Common;
using FinanceTracker.Domain.Transactions;
using FluentAssertions;

namespace FinanceTracker.Domain.UnitTests.Transactions
{
    public class TransactionTests
    {
        private static readonly AccountId SampleAccountId = AccountId.New();
        private static readonly DateOnly Today = DateOnly.FromDateTime(DateTime.UtcNow);

        [Fact]
        public void Create_WithValidData_Succeeds()
        {
            var transaction = Transaction.Create(
                SampleAccountId,
                Money.Create(50m, "USD"),
                TransactionType.Expense,
                "Groceries",
                Today);

            transaction.AccountId.Should().Be(SampleAccountId);
            transaction.Amount.Should().Be(Money.Create(50m, "USD"));
            transaction.Type.Should().Be(TransactionType.Expense);
            transaction.Description.Should().Be("Groceries");
            transaction.OccurredOn.Should().Be(Today);
            transaction.CategoryId.Should().BeNull();
        }

        [Fact]
        public void Create_WithZeroAmount_Throws()
        {
            var act = () => Transaction.Create(
                SampleAccountId, Money.Create(0m, "USD"), TransactionType.Expense, "Groceries", Today);

            act.Should().Throw<ArgumentException>();
        }

        [Fact]
        public void Create_WithNegativeAmount_Throws()
        {
            var act = () => Transaction.Create(
                SampleAccountId, Money.Create(-10m, "USD"), TransactionType.Expense, "Groceries", Today);

            act.Should().Throw<ArgumentException>();
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        public void Create_WithMissingDescription_Throws(string? description)
        {
            var act = () => Transaction.Create(
                SampleAccountId, Money.Create(10m, "USD"), TransactionType.Expense, description!, Today);

            act.Should().Throw<ArgumentException>();
        }

        [Fact]
        public void Create_WithFutureDate_Throws()
        {
            var future = Today.AddDays(1);

            var act = () => Transaction.Create(
                SampleAccountId, Money.Create(10m, "USD"), TransactionType.Expense, "Groceries", future);

            act.Should().Throw<ArgumentException>();
        }

        [Fact]
        public void SignedAmount_ForExpense_IsNegative()
        {
            var transaction = Transaction.Create(
                SampleAccountId, Money.Create(50m, "USD"), TransactionType.Expense, "Groceries", Today);

            transaction.SignedAmount.Should().Be(Money.Create(-50m, "USD"));
        }

        [Fact]
        public void SignedAmount_ForIncome_IsPositive()
        {
            var transaction = Transaction.Create(
                SampleAccountId, Money.Create(50m, "USD"), TransactionType.Income, "Paycheck", Today);

            transaction.SignedAmount.Should().Be(Money.Create(50m, "USD"));
        }

        [Fact]
        public void Recategorize_SetsCategoryId()
        {
            var transaction = Transaction.Create(
                SampleAccountId, Money.Create(50m, "USD"), TransactionType.Expense, "Groceries", Today);
            var categoryId = CategoryId.New();

            transaction.Recategorize(categoryId);

            transaction.CategoryId.Should().Be(categoryId);
        }

        [Fact]
        public void Recategorize_WithNull_ClearsCategory()
        {
            var transaction = Transaction.Create(
                SampleAccountId, Money.Create(50m, "USD"), TransactionType.Expense, "Groceries", Today,
                CategoryId.New());

            transaction.Recategorize(null);

            transaction.CategoryId.Should().BeNull();
        }

        [Fact]
        public void UpdateAmount_WithValidAmount_UpdatesAmount()
        {
            var transaction = Transaction.Create(
                SampleAccountId, Money.Create(50m, "USD"), TransactionType.Expense, "Groceries", Today);

            transaction.UpdateAmount(Money.Create(75m, "USD"));

            transaction.Amount.Should().Be(Money.Create(75m, "USD"));
        }

        [Fact]
        public void UpdateAmount_WithZero_Throws()
        {
            var transaction = Transaction.Create(
                SampleAccountId, Money.Create(50m, "USD"), TransactionType.Expense, "Groceries", Today);

            var act = () => transaction.UpdateAmount(Money.Create(0m, "USD"));

            act.Should().Throw<ArgumentException>();
        }

        [Fact]
        public void UpdateDescription_WithValidText_UpdatesDescription()
        {
            var transaction = Transaction.Create(
                SampleAccountId, Money.Create(50m, "USD"), TransactionType.Expense, "Groceries", Today);

            transaction.UpdateDescription("Weekly groceries");

            transaction.Description.Should().Be("Weekly groceries");
        }

        [Fact]
        public void UpdateOccurredOn_WithFutureDate_Throws()
        {
            var transaction = Transaction.Create(
                SampleAccountId, Money.Create(50m, "USD"), TransactionType.Expense, "Groceries", Today);

            var act = () => transaction.UpdateOccurredOn(Today.AddDays(1));

            act.Should().Throw<ArgumentException>();
        }

        [Fact]
        public void UpdateOccurredOn_WithPastDate_Updates()
        {
            var transaction = Transaction.Create(
                SampleAccountId, Money.Create(50m, "USD"), TransactionType.Expense, "Groceries", Today);
            var pastDate = Today.AddDays(-3);

            transaction.UpdateOccurredOn(pastDate);

            transaction.OccurredOn.Should().Be(pastDate);
        }
    }
}
