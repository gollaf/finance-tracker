using FinanceTracker.Application.Accounts;
using FinanceTracker.Application.Accounts.GetAccountBalance;
using FinanceTracker.Application.Common;
using FinanceTracker.Application.Transactions;
using FinanceTracker.Domain.Accounts;
using FinanceTracker.Domain.Common;
using FinanceTracker.Domain.Transactions;
using FluentAssertions;
using NSubstitute;

namespace FinanceTracker.Application.UnitTests.Accounts.GetAccountBalance
{
    public class GetAccountBalanceQueryHandlerTests
    {
        private static readonly DateOnly Today = DateOnly.FromDateTime(DateTime.UtcNow);

        private static Account NewAccount() => Account.Create("Checking", AccountType.Checking, "USD");

        [Fact]
        public async Task Handle_WithNoTransactions_ReturnsZeroBalance()
        {
            var account = NewAccount();

            var accountRepository = Substitute.For<IAccountRepository>();
            accountRepository.GetByIdAsync(account.Id, Arg.Any<CancellationToken>()).Returns(account);

            var transactionRepository = Substitute.For<ITransactionRepository>();
            transactionRepository
                .GetByAccountIdAsync(account.Id, Arg.Any<CancellationToken>())
                .Returns(Array.Empty<Transaction>());

            var handler = new GetAccountBalanceQueryHandler(accountRepository, transactionRepository);
            var query = new GetAccountBalanceQuery(account.Id);

            var result = await handler.Handle(query, CancellationToken.None);

            result.IsSuccess.Should().BeTrue();
            result.Value.Should().Be(Money.Zero("USD"));
        }

        [Fact]
        public async Task Handle_WithIncomeAndExpenseTransactions_ReturnsNetBalance()
        {
            var account = NewAccount();
            var transactions = new[]
            {
                Transaction.Create(account.Id, Money.Create(100m, "USD"), TransactionType.Income, "Salary", Today),
                Transaction.Create(account.Id, Money.Create(30m, "USD"), TransactionType.Expense, "Groceries", Today),
            };

            var accountRepository = Substitute.For<IAccountRepository>();
            accountRepository.GetByIdAsync(account.Id, Arg.Any<CancellationToken>()).Returns(account);

            var transactionRepository = Substitute.For<ITransactionRepository>();
            transactionRepository
                .GetByAccountIdAsync(account.Id, Arg.Any<CancellationToken>())
                .Returns(transactions);

            var handler = new GetAccountBalanceQueryHandler(accountRepository, transactionRepository);
            var query = new GetAccountBalanceQuery(account.Id);

            var result = await handler.Handle(query, CancellationToken.None);

            result.IsSuccess.Should().BeTrue();
            result.Value.Should().Be(Money.Create(70m, "USD"));
        }

        [Fact]
        public async Task Handle_WithUnknownAccount_ReturnsNotFound()
        {
            var accountRepository = Substitute.For<IAccountRepository>();
            accountRepository
                .GetByIdAsync(Arg.Any<AccountId>(), Arg.Any<CancellationToken>())
                .Returns((Account?)null);

            var transactionRepository = Substitute.For<ITransactionRepository>();

            var handler = new GetAccountBalanceQueryHandler(accountRepository, transactionRepository);
            var query = new GetAccountBalanceQuery(AccountId.New());

            var result = await handler.Handle(query, CancellationToken.None);

            result.IsFailure.Should().BeTrue();
            result.Error.Type.Should().Be(ErrorType.NotFound);
        }
    }
}
