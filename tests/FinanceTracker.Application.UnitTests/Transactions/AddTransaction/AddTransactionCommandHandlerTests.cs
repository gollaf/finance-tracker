using FinanceTracker.Application.Accounts;
using FinanceTracker.Application.Common;
using FinanceTracker.Application.Transactions;
using FinanceTracker.Application.Transactions.AddTransaction;
using FinanceTracker.Domain.Accounts;
using FinanceTracker.Domain.Common;
using FinanceTracker.Domain.Transactions;
using FluentAssertions;
using NSubstitute;

namespace FinanceTracker.Application.UnitTests.Transactions.AddTransaction
{
    public class AddTransactionCommandHandlerTests
    {
        private static readonly DateOnly Today = DateOnly.FromDateTime(DateTime.UtcNow);

        [Fact]
        public async Task Handle_WithValidCommand_SavesTransactionInAccountsCurrency()
        {
            var account = Account.Create("Checking", AccountType.Checking, "USD");
            var accountRepository = Substitute.For<IAccountRepository>();
            accountRepository.GetByIdAsync(account.Id, Arg.Any<CancellationToken>()).Returns(account);

            Transaction? savedTransaction = null;
            var transactionRepository = Substitute.For<ITransactionRepository>();
            transactionRepository
                .AddAsync(Arg.Do<Transaction>(t => savedTransaction = t), Arg.Any<CancellationToken>());

            var handler = new AddTransactionCommandHandler(accountRepository, transactionRepository);
            var command = new AddTransactionCommand(account.Id, 42.50m, TransactionType.Expense, "Groceries", Today);

            var result = await handler.Handle(command, CancellationToken.None);

            result.IsSuccess.Should().BeTrue();
            savedTransaction.Should().NotBeNull();
            savedTransaction!.AccountId.Should().Be(account.Id);
            savedTransaction.Amount.Should().Be(Money.Create(42.50m, "USD"));
            savedTransaction.Type.Should().Be(TransactionType.Expense);
            savedTransaction.Description.Should().Be("Groceries");
            savedTransaction.OccurredOn.Should().Be(Today);
            savedTransaction.CategoryId.Should().BeNull();
            result.Value.Should().Be(savedTransaction.Id);
        }

        [Fact]
        public async Task Handle_WithUnknownAccount_ReturnsNotFound()
        {
            var accountRepository = Substitute.For<IAccountRepository>();
            accountRepository
                .GetByIdAsync(Arg.Any<AccountId>(), Arg.Any<CancellationToken>())
                .Returns((Account?)null);
            var transactionRepository = Substitute.For<ITransactionRepository>();

            var handler = new AddTransactionCommandHandler(accountRepository, transactionRepository);
            var command = new AddTransactionCommand(
                AccountId.New(), 10m, TransactionType.Expense, "Groceries", Today);

            var result = await handler.Handle(command, CancellationToken.None);

            result.IsFailure.Should().BeTrue();
            result.Error.Type.Should().Be(ErrorType.NotFound);
            await transactionRepository.DidNotReceive()
                .AddAsync(Arg.Any<Transaction>(), Arg.Any<CancellationToken>());
        }

        [Fact]
        public async Task Handle_WithClosedAccount_ReturnsConflict()
        {
            var account = Account.Create("Checking", AccountType.Checking, "USD");
            account.Close();
            var accountRepository = Substitute.For<IAccountRepository>();
            accountRepository.GetByIdAsync(account.Id, Arg.Any<CancellationToken>()).Returns(account);
            var transactionRepository = Substitute.For<ITransactionRepository>();

            var handler = new AddTransactionCommandHandler(accountRepository, transactionRepository);
            var command = new AddTransactionCommand(account.Id, 10m, TransactionType.Expense, "Groceries", Today);

            var result = await handler.Handle(command, CancellationToken.None);

            result.IsFailure.Should().BeTrue();
            result.Error.Type.Should().Be(ErrorType.Conflict);
            await transactionRepository.DidNotReceive()
                .AddAsync(Arg.Any<Transaction>(), Arg.Any<CancellationToken>());
        }
    }
}
