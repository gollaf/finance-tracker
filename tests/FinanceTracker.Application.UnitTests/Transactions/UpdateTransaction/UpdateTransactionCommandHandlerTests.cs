using FinanceTracker.Application.Common;
using FinanceTracker.Application.Transactions;
using FinanceTracker.Application.Transactions.UpdateTransaction;
using FinanceTracker.Domain.Common;
using FinanceTracker.Domain.Transactions;
using FluentAssertions;
using NSubstitute;

namespace FinanceTracker.Application.UnitTests.Transactions.UpdateTransaction
{
    public class UpdateTransactionCommandHandlerTests
    {
        private static readonly DateOnly Today = DateOnly.FromDateTime(DateTime.UtcNow);

        [Fact]
        public async Task Handle_WithValidCommand_UpdatesTransactionFieldsAndSaves()
        {
            var transaction = Transaction.Create(
                AccountId.New(),
                Money.Create(20m, "USD"),
                TransactionType.Expense,
                "Old description",
                Today.AddDays(-1));

            var repository = Substitute.For<ITransactionRepository>();
            repository.GetByIdAsync(transaction.Id, Arg.Any<CancellationToken>()).Returns(transaction);

            var handler = new UpdateTransactionCommandHandler(repository);
            var command = new UpdateTransactionCommand(transaction.Id, 35m, "New description", Today);

            var result = await handler.Handle(command, CancellationToken.None);

            result.IsSuccess.Should().BeTrue();
            transaction.Amount.Should().Be(Money.Create(35m, "USD"));
            transaction.Description.Should().Be("New description");
            transaction.OccurredOn.Should().Be(Today);
            await repository.Received(1).UpdateAsync(transaction, Arg.Any<CancellationToken>());
        }

        [Fact]
        public async Task Handle_WithUnknownTransaction_ReturnsNotFound()
        {
            var repository = Substitute.For<ITransactionRepository>();
            repository
                .GetByIdAsync(Arg.Any<TransactionId>(), Arg.Any<CancellationToken>())
                .Returns((Transaction?)null);

            var handler = new UpdateTransactionCommandHandler(repository);
            var command = new UpdateTransactionCommand(TransactionId.New(), 35m, "New description", Today);

            var result = await handler.Handle(command, CancellationToken.None);

            result.IsFailure.Should().BeTrue();
            result.Error.Type.Should().Be(ErrorType.NotFound);
            await repository.DidNotReceive().UpdateAsync(Arg.Any<Transaction>(), Arg.Any<CancellationToken>());
        }
    }
}
