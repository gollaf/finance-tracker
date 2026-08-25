using FinanceTracker.Application.Accounts;
using FinanceTracker.Application.Categorization;
using FinanceTracker.Application.Common;
using FinanceTracker.Application.Transactions;
using FinanceTracker.Application.Transactions.ImportTransactionsFromCsv;
using FinanceTracker.Domain.Accounts;
using FinanceTracker.Domain.Categorization;
using FinanceTracker.Domain.Common;
using FinanceTracker.Domain.Transactions;
using FluentAssertions;
using NSubstitute;

namespace FinanceTracker.Application.UnitTests.Transactions.ImportTransactionsFromCsv
{
    public class ImportTransactionsFromCsvCommandHandlerTests
    {
        private static readonly DateOnly Today = DateOnly.FromDateTime(DateTime.UtcNow);

        private static Account NewAccount() => Account.Create("Checking", AccountType.Checking, "USD");

        [Fact]
        public async Task Handle_WithMixOfValidAndInvalidRows_ImportsValidRowsAndReportsErrors()
        {
            var account = NewAccount();
            var rows = new[]
            {
                new CsvTransactionRow(20m, TransactionType.Expense, "Coffee", Today),
                new CsvTransactionRow(15m, TransactionType.Expense, "   ", Today),
                new CsvTransactionRow(50m, TransactionType.Income, "Refund", Today),
            };

            var accountRepository = Substitute.For<IAccountRepository>();
            accountRepository.GetByIdAsync(account.Id, Arg.Any<CancellationToken>()).Returns(account);

            var transactionRepository = Substitute.For<ITransactionRepository>();

            var ruleRepository = Substitute.For<ICategorizationRuleRepository>();
            ruleRepository.GetAllAsync(Arg.Any<CancellationToken>()).Returns(Array.Empty<CategorizationRule>());

            var handler = new ImportTransactionsFromCsvCommandHandler(
                accountRepository, transactionRepository, ruleRepository);
            var command = new ImportTransactionsFromCsvCommand(account.Id, rows);

            var result = await handler.Handle(command, CancellationToken.None);

            result.IsSuccess.Should().BeTrue();
            result.Value.ImportedTransactionIds.Should().HaveCount(2);
            result.Value.Errors.Should().ContainSingle(e => e.RowIndex == 1);
            await transactionRepository.Received(2).AddAsync(Arg.Any<Transaction>(), Arg.Any<CancellationToken>());
        }

        [Fact]
        public async Task Handle_WithUnknownAccount_ReturnsNotFound()
        {
            var accountRepository = Substitute.For<IAccountRepository>();
            accountRepository
                .GetByIdAsync(Arg.Any<AccountId>(), Arg.Any<CancellationToken>())
                .Returns((Account?)null);

            var transactionRepository = Substitute.For<ITransactionRepository>();
            var ruleRepository = Substitute.For<ICategorizationRuleRepository>();

            var handler = new ImportTransactionsFromCsvCommandHandler(
                accountRepository, transactionRepository, ruleRepository);
            var command = new ImportTransactionsFromCsvCommand(
                AccountId.New(), new[] { new CsvTransactionRow(20m, TransactionType.Expense, "Coffee", Today) });

            var result = await handler.Handle(command, CancellationToken.None);

            result.IsFailure.Should().BeTrue();
            result.Error.Type.Should().Be(ErrorType.NotFound);
            await transactionRepository.DidNotReceive().AddAsync(Arg.Any<Transaction>(), Arg.Any<CancellationToken>());
        }

        [Fact]
        public async Task Handle_WithClosedAccount_ReturnsConflict()
        {
            var account = NewAccount();
            account.Close();

            var accountRepository = Substitute.For<IAccountRepository>();
            accountRepository.GetByIdAsync(account.Id, Arg.Any<CancellationToken>()).Returns(account);

            var transactionRepository = Substitute.For<ITransactionRepository>();
            var ruleRepository = Substitute.For<ICategorizationRuleRepository>();

            var handler = new ImportTransactionsFromCsvCommandHandler(
                accountRepository, transactionRepository, ruleRepository);
            var command = new ImportTransactionsFromCsvCommand(
                account.Id, new[] { new CsvTransactionRow(20m, TransactionType.Expense, "Coffee", Today) });

            var result = await handler.Handle(command, CancellationToken.None);

            result.IsFailure.Should().BeTrue();
            result.Error.Type.Should().Be(ErrorType.Conflict);
            await transactionRepository.DidNotReceive().AddAsync(Arg.Any<Transaction>(), Arg.Any<CancellationToken>());
        }

        [Fact]
        public async Task Handle_WithMatchingCategorizationRule_AssignsCategoryFromRule()
        {
            var account = NewAccount();
            var categoryId = CategoryId.New();
            var rule = CategorizationRule.Create("coffee", categoryId, priority: 1);

            var accountRepository = Substitute.For<IAccountRepository>();
            accountRepository.GetByIdAsync(account.Id, Arg.Any<CancellationToken>()).Returns(account);

            Transaction? savedTransaction = null;
            var transactionRepository = Substitute.For<ITransactionRepository>();
            transactionRepository
                .AddAsync(Arg.Do<Transaction>(t => savedTransaction = t), Arg.Any<CancellationToken>())
                .Returns(Task.CompletedTask);

            var ruleRepository = Substitute.For<ICategorizationRuleRepository>();
            ruleRepository.GetAllAsync(Arg.Any<CancellationToken>()).Returns(new[] { rule });

            var handler = new ImportTransactionsFromCsvCommandHandler(
                accountRepository, transactionRepository, ruleRepository);
            var command = new ImportTransactionsFromCsvCommand(
                account.Id, new[] { new CsvTransactionRow(20m, TransactionType.Expense, "Coffee Shop", Today) });

            var result = await handler.Handle(command, CancellationToken.None);

            result.IsSuccess.Should().BeTrue();
            savedTransaction!.CategoryId.Should().Be(categoryId);
        }
    }
}
