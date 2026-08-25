using FinanceTracker.Application.Common;
using FinanceTracker.Application.Transactions;
using FinanceTracker.Application.Transactions.DeleteTransaction;
using FinanceTracker.Domain.Common;
using FinanceTracker.Domain.Transactions;
using FluentAssertions;
using NSubstitute;

namespace FinanceTracker.Application.UnitTests.Transactions.DeleteTransaction
{
    public class DeleteTransactionCommandHandlerTests
    {
        private static readonly DateOnly Today = DateOnly.FromDateTime(DateTime.UtcNow);

        [Fact]
        public async Task Handle_WithExistingTransaction_DeletesItAndSucceeds()
        {
            var transaction = Transaction.Create(
                AccountId.New(), Money.Create(20m, "USD"), TransactionType.Expense, "Coffee", Today);

            var repository = Substitute.For<ITransactionRepository>();
            repository.GetByIdAsync(transaction.Id, Arg.Any<CancellationToken>()).Returns(transaction);

            var handler = new DeleteTransactionCommandHandler(repository);
            var command = new DeleteTransactionCommand(transaction.Id);

            var result = await handler.Handle(command, CancellationToken.None);

            result.IsSuccess.Should().BeTrue();
            await repository.Received(1).DeleteAsync(transaction, Arg.Any<CancellationToken>());
        }

        [Fact]
        public async Task Handle_WithUnknownTransaction_ReturnsNotFound()
        {
            var repository = Substitute.For<ITransactionRepository>();
            repository
                .GetByIdAsync(Arg.Any<TransactionId>(), Arg.Any<CancellationToken>())
                .Returns((Transaction?)null);

            var handler = new DeleteTransactionCommandHandler(repository);
            var command = new DeleteTransactionCommand(TransactionId.New());

            var result = await handler.Handle(command, CancellationToken.None);

            result.IsFailure.Should().BeTrue();
            result.Error.Type.Should().Be(ErrorType.NotFound);
            await repository.DidNotReceive().DeleteAsync(Arg.Any<Transaction>(), Arg.Any<CancellationToken>());
        }
    }
}
