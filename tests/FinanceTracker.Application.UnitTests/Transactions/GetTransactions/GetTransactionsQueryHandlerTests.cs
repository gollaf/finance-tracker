using FinanceTracker.Application.Accounts;
using FinanceTracker.Application.Common;
using FinanceTracker.Application.Transactions;
using FinanceTracker.Application.Transactions.GetTransactions;
using FinanceTracker.Domain.Accounts;
using FinanceTracker.Domain.Common;
using FinanceTracker.Domain.Transactions;
using FluentAssertions;
using NSubstitute;

namespace FinanceTracker.Application.UnitTests.Transactions.GetTransactions
{
    public class GetTransactionsQueryHandlerTests
    {
        private static Account NewAccount() => Account.Create("Checking", AccountType.Checking, "USD");

        [Fact]
        public async Task Handle_WithNoDateFilter_ReturnsAllTransactionsMostRecentFirst()
        {
            var account = NewAccount();
            var older = Transaction.Create(
                account.Id, Money.Create(10m, "USD"), TransactionType.Expense, "Old", new DateOnly(2026, 1, 1));
            var newer = Transaction.Create(
                account.Id, Money.Create(20m, "USD"), TransactionType.Expense, "New", new DateOnly(2026, 6, 1));

            var accountRepository = Substitute.For<IAccountRepository>();
            accountRepository.GetByIdAsync(account.Id, Arg.Any<CancellationToken>()).Returns(account);

            var transactionRepository = Substitute.For<ITransactionRepository>();
            transactionRepository
                .GetByAccountIdAsync(account.Id, Arg.Any<CancellationToken>())
                .Returns(new[] { older, newer });

            var handler = new GetTransactionsQueryHandler(accountRepository, transactionRepository);
            var query = new GetTransactionsQuery(account.Id);

            var result = await handler.Handle(query, CancellationToken.None);

            result.IsSuccess.Should().BeTrue();
            result.Value.Should().HaveCount(2);
            result.Value[0].Id.Should().Be(newer.Id);
            result.Value[1].Id.Should().Be(older.Id);
        }

        [Fact]
        public async Task Handle_WithDateRangeFilter_ReturnsOnlyTransactionsInRange()
        {
            var account = NewAccount();
            var january = Transaction.Create(
                account.Id, Money.Create(10m, "USD"), TransactionType.Expense, "January", new DateOnly(2026, 1, 15));
            var june = Transaction.Create(
                account.Id, Money.Create(20m, "USD"), TransactionType.Expense, "June", new DateOnly(2026, 6, 15));

            var accountRepository = Substitute.For<IAccountRepository>();
            accountRepository.GetByIdAsync(account.Id, Arg.Any<CancellationToken>()).Returns(account);

            var transactionRepository = Substitute.For<ITransactionRepository>();
            transactionRepository
                .GetByAccountIdAsync(account.Id, Arg.Any<CancellationToken>())
                .Returns(new[] { january, june });

            var handler = new GetTransactionsQueryHandler(accountRepository, transactionRepository);
            var query = new GetTransactionsQuery(account.Id, new DateOnly(2026, 6, 1), new DateOnly(2026, 6, 30));

            var result = await handler.Handle(query, CancellationToken.None);

            result.IsSuccess.Should().BeTrue();
            result.Value.Should().ContainSingle(t => t.Id == june.Id);
        }

        [Fact]
        public async Task Handle_WithUnknownAccount_ReturnsNotFound()
        {
            var accountRepository = Substitute.For<IAccountRepository>();
            accountRepository
                .GetByIdAsync(Arg.Any<AccountId>(), Arg.Any<CancellationToken>())
                .Returns((Account?)null);

            var transactionRepository = Substitute.For<ITransactionRepository>();

            var handler = new GetTransactionsQueryHandler(accountRepository, transactionRepository);
            var query = new GetTransactionsQuery(AccountId.New());

            var result = await handler.Handle(query, CancellationToken.None);

            result.IsFailure.Should().BeTrue();
            result.Error.Type.Should().Be(ErrorType.NotFound);
        }
    }
}
