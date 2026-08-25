using FinanceTracker.Application.Categories;
using FinanceTracker.Application.Common;
using FinanceTracker.Application.Transactions;
using FinanceTracker.Application.Transactions.CategorizeTransaction;
using FinanceTracker.Domain.Categories;
using FinanceTracker.Domain.Common;
using FinanceTracker.Domain.Transactions;
using FluentAssertions;
using NSubstitute;

namespace FinanceTracker.Application.UnitTests.Transactions.CategorizeTransaction
{
    public class CategorizeTransactionCommandHandlerTests
    {
        private static readonly DateOnly Today = DateOnly.FromDateTime(DateTime.UtcNow);

        private static Transaction NewTransaction(CategoryId? categoryId = null) =>
            Transaction.Create(
                AccountId.New(), Money.Create(20m, "USD"), TransactionType.Expense, "Coffee", Today, categoryId);

        [Fact]
        public async Task Handle_WithValidCategoryId_AssignsCategoryAndSaves()
        {
            var transaction = NewTransaction();
            var category = Category.Create("Dining", parentCategoryId: null);

            var transactionRepository = Substitute.For<ITransactionRepository>();
            transactionRepository.GetByIdAsync(transaction.Id, Arg.Any<CancellationToken>()).Returns(transaction);

            var categoryRepository = Substitute.For<ICategoryRepository>();
            categoryRepository.GetByIdAsync(category.Id, Arg.Any<CancellationToken>()).Returns(category);

            var handler = new CategorizeTransactionCommandHandler(transactionRepository, categoryRepository);
            var command = new CategorizeTransactionCommand(transaction.Id, category.Id);

            var result = await handler.Handle(command, CancellationToken.None);

            result.IsSuccess.Should().BeTrue();
            transaction.CategoryId.Should().Be(category.Id);
            await transactionRepository.Received(1).UpdateAsync(transaction, Arg.Any<CancellationToken>());
        }

        [Fact]
        public async Task Handle_WithNullCategoryId_ClearsCategoryWithoutLookup()
        {
            var transaction = NewTransaction(CategoryId.New());

            var transactionRepository = Substitute.For<ITransactionRepository>();
            transactionRepository.GetByIdAsync(transaction.Id, Arg.Any<CancellationToken>()).Returns(transaction);

            var categoryRepository = Substitute.For<ICategoryRepository>();

            var handler = new CategorizeTransactionCommandHandler(transactionRepository, categoryRepository);
            var command = new CategorizeTransactionCommand(transaction.Id, null);

            var result = await handler.Handle(command, CancellationToken.None);

            result.IsSuccess.Should().BeTrue();
            transaction.CategoryId.Should().BeNull();
            await categoryRepository.DidNotReceive().GetByIdAsync(Arg.Any<CategoryId>(), Arg.Any<CancellationToken>());
            await transactionRepository.Received(1).UpdateAsync(transaction, Arg.Any<CancellationToken>());
        }

        [Fact]
        public async Task Handle_WithUnknownTransaction_ReturnsNotFound()
        {
            var transactionRepository = Substitute.For<ITransactionRepository>();
            transactionRepository
                .GetByIdAsync(Arg.Any<TransactionId>(), Arg.Any<CancellationToken>())
                .Returns((Transaction?)null);

            var categoryRepository = Substitute.For<ICategoryRepository>();

            var handler = new CategorizeTransactionCommandHandler(transactionRepository, categoryRepository);
            var command = new CategorizeTransactionCommand(TransactionId.New(), CategoryId.New());

            var result = await handler.Handle(command, CancellationToken.None);

            result.IsFailure.Should().BeTrue();
            result.Error.Type.Should().Be(ErrorType.NotFound);
            await transactionRepository.DidNotReceive().UpdateAsync(Arg.Any<Transaction>(), Arg.Any<CancellationToken>());
        }

        [Fact]
        public async Task Handle_WithUnknownCategory_ReturnsNotFound()
        {
            var transaction = NewTransaction();

            var transactionRepository = Substitute.For<ITransactionRepository>();
            transactionRepository.GetByIdAsync(transaction.Id, Arg.Any<CancellationToken>()).Returns(transaction);

            var categoryRepository = Substitute.For<ICategoryRepository>();
            categoryRepository
                .GetByIdAsync(Arg.Any<CategoryId>(), Arg.Any<CancellationToken>())
                .Returns((Category?)null);

            var handler = new CategorizeTransactionCommandHandler(transactionRepository, categoryRepository);
            var command = new CategorizeTransactionCommand(transaction.Id, CategoryId.New());

            var result = await handler.Handle(command, CancellationToken.None);

            result.IsFailure.Should().BeTrue();
            result.Error.Type.Should().Be(ErrorType.NotFound);
            await transactionRepository.DidNotReceive().UpdateAsync(Arg.Any<Transaction>(), Arg.Any<CancellationToken>());
        }
    }
}
