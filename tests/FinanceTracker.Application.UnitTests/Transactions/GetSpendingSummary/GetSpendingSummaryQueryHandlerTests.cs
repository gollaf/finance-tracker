using FinanceTracker.Application.Accounts;
using FinanceTracker.Application.Common;
using FinanceTracker.Application.Transactions;
using FinanceTracker.Application.Transactions.GetSpendingSummary;
using FinanceTracker.Domain.Accounts;
using FinanceTracker.Domain.Common;
using FinanceTracker.Domain.Transactions;
using FluentAssertions;
using NSubstitute;

namespace FinanceTracker.Application.UnitTests.Transactions.GetSpendingSummary
{
    public class GetSpendingSummaryQueryHandlerTests
    {
        private static Account NewAccount() => Account.Create("Checking", AccountType.Checking, "USD");

        [Fact]
        public async Task Handle_GroupsExpensesByCategoryWithinPeriod()
        {
            var account = NewAccount();
            var groceries = CategoryId.New();
            var dining = CategoryId.New();

            var transactions = new[]
            {
                Transaction.Create(
                    account.Id, Money.Create(30m, "USD"), TransactionType.Expense, "Store",
                    new DateOnly(2026, 6, 5), groceries),
                Transaction.Create(
                    account.Id, Money.Create(20m, "USD"), TransactionType.Expense, "Market",
                    new DateOnly(2026, 6, 10), groceries),
                Transaction.Create(
                    account.Id, Money.Create(15m, "USD"), TransactionType.Expense, "Cafe",
                    new DateOnly(2026, 6, 15), dining),
                Transaction.Create(
                    account.Id, Money.Create(500m, "USD"), TransactionType.Income, "Salary",
                    new DateOnly(2026, 6, 1)),
                Transaction.Create(
                    account.Id, Money.Create(999m, "USD"), TransactionType.Expense, "Outside period",
                    new DateOnly(2026, 5, 30), groceries),
            };

            var accountRepository = Substitute.For<IAccountRepository>();
            accountRepository.GetByIdAsync(account.Id, Arg.Any<CancellationToken>()).Returns(account);

            var transactionRepository = Substitute.For<ITransactionRepository>();
            transactionRepository
                .GetByAccountIdAsync(account.Id, Arg.Any<CancellationToken>())
                .Returns(transactions);

            var handler = new GetSpendingSummaryQueryHandler(accountRepository, transactionRepository);
            var query = new GetSpendingSummaryQuery(account.Id, 2026, 6);

            var result = await handler.Handle(query, CancellationToken.None);

            result.IsSuccess.Should().BeTrue();
            result.Value.Should().HaveCount(2);
            result.Value[0].CategoryId.Should().Be(groceries);
            result.Value[0].Total.Should().Be(Money.Create(50m, "USD"));
            result.Value[1].CategoryId.Should().Be(dining);
            result.Value[1].Total.Should().Be(Money.Create(15m, "USD"));
        }

        [Fact]
        public async Task Handle_WithUncategorizedExpense_GroupsUnderNullCategory()
        {
            var account = NewAccount();
            var transaction = Transaction.Create(
                account.Id, Money.Create(10m, "USD"), TransactionType.Expense, "Misc", new DateOnly(2026, 6, 1));

            var accountRepository = Substitute.For<IAccountRepository>();
            accountRepository.GetByIdAsync(account.Id, Arg.Any<CancellationToken>()).Returns(account);

            var transactionRepository = Substitute.For<ITransactionRepository>();
            transactionRepository
                .GetByAccountIdAsync(account.Id, Arg.Any<CancellationToken>())
                .Returns(new[] { transaction });

            var handler = new GetSpendingSummaryQueryHandler(accountRepository, transactionRepository);
            var query = new GetSpendingSummaryQuery(account.Id, 2026, 6);

            var result = await handler.Handle(query, CancellationToken.None);

            result.IsSuccess.Should().BeTrue();
            result.Value.Should().ContainSingle(s => s.CategoryId == null && s.Total == Money.Create(10m, "USD"));
        }

        [Fact]
        public async Task Handle_WithUnknownAccount_ReturnsNotFound()
        {
            var accountRepository = Substitute.For<IAccountRepository>();
            accountRepository
                .GetByIdAsync(Arg.Any<AccountId>(), Arg.Any<CancellationToken>())
                .Returns((Account?)null);

            var transactionRepository = Substitute.For<ITransactionRepository>();

            var handler = new GetSpendingSummaryQueryHandler(accountRepository, transactionRepository);
            var query = new GetSpendingSummaryQuery(AccountId.New(), 2026, 6);

            var result = await handler.Handle(query, CancellationToken.None);

            result.IsFailure.Should().BeTrue();
            result.Error.Type.Should().Be(ErrorType.NotFound);
        }
    }
}
