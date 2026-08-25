using FinanceTracker.Application.Budgets;
using FinanceTracker.Application.Budgets.UpdateBudget;
using FinanceTracker.Application.Common;
using FinanceTracker.Domain.Budgets;
using FinanceTracker.Domain.Common;
using FluentAssertions;
using NSubstitute;

namespace FinanceTracker.Application.UnitTests.Budgets.UpdateBudget
{
    public class UpdateBudgetCommandHandlerTests
    {
        [Fact]
        public async Task Handle_WithValidCommand_UpdatesLimitAndSaves()
        {
            var budget = Budget.Create(CategoryId.New(), BudgetPeriod.Create(2026, 8), Money.Create(300m, "USD"));

            var repository = Substitute.For<IBudgetRepository>();
            repository.GetByIdAsync(budget.Id, Arg.Any<CancellationToken>()).Returns(budget);

            var handler = new UpdateBudgetCommandHandler(repository);
            var command = new UpdateBudgetCommand(budget.Id, 500m);

            var result = await handler.Handle(command, CancellationToken.None);

            result.IsSuccess.Should().BeTrue();
            budget.LimitAmount.Should().Be(Money.Create(500m, "USD"));
            await repository.Received(1).UpdateAsync(budget, Arg.Any<CancellationToken>());
        }

        [Fact]
        public async Task Handle_WithUnknownBudget_ReturnsNotFound()
        {
            var repository = Substitute.For<IBudgetRepository>();
            repository.GetByIdAsync(Arg.Any<BudgetId>(), Arg.Any<CancellationToken>()).Returns((Budget?)null);

            var handler = new UpdateBudgetCommandHandler(repository);
            var command = new UpdateBudgetCommand(BudgetId.New(), 500m);

            var result = await handler.Handle(command, CancellationToken.None);

            result.IsFailure.Should().BeTrue();
            result.Error.Type.Should().Be(ErrorType.NotFound);
            await repository.DidNotReceive().UpdateAsync(Arg.Any<Budget>(), Arg.Any<CancellationToken>());
        }
    }
}
