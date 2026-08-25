using FinanceTracker.Application.Budgets.CreateBudget;
using FinanceTracker.Application.Categories;
using FinanceTracker.Application.Common;
using FinanceTracker.Domain.Budgets;
using FinanceTracker.Domain.Categories;
using FinanceTracker.Domain.Common;
using FluentAssertions;
using NSubstitute;

namespace FinanceTracker.Application.UnitTests.Budgets.CreateBudget
{
    public class CreateBudgetCommandHandlerTests
    {
        [Fact]
        public async Task Handle_WithValidCommand_CreatesBudgetAndSaves()
        {
            var category = Category.Create("Groceries", parentCategoryId: null);

            var categoryRepository = Substitute.For<ICategoryRepository>();
            categoryRepository.GetByIdAsync(category.Id, Arg.Any<CancellationToken>()).Returns(category);

            var budgetRepository = Substitute.For<IBudgetRepository>();
            budgetRepository
                .GetByCategoryAndPeriodAsync(category.Id, Arg.Any<BudgetPeriod>(), Arg.Any<CancellationToken>())
                .Returns((Budget?)null);

            Budget? savedBudget = null;
            budgetRepository
                .AddAsync(Arg.Do<Budget>(b => savedBudget = b), Arg.Any<CancellationToken>())
                .Returns(Task.CompletedTask);

            var handler = new CreateBudgetCommandHandler(categoryRepository, budgetRepository);
            var command = new CreateBudgetCommand(category.Id, 2026, 8, 500m, "USD");

            var result = await handler.Handle(command, CancellationToken.None);

            result.IsSuccess.Should().BeTrue();
            result.Value.Should().Be(savedBudget!.Id);
            savedBudget.CategoryId.Should().Be(category.Id);
            savedBudget.Period.Should().Be(BudgetPeriod.Create(2026, 8));
            savedBudget.LimitAmount.Should().Be(Money.Create(500m, "USD"));
        }

        [Fact]
        public async Task Handle_WithUnknownCategory_ReturnsNotFound()
        {
            var categoryRepository = Substitute.For<ICategoryRepository>();
            categoryRepository
                .GetByIdAsync(Arg.Any<CategoryId>(), Arg.Any<CancellationToken>())
                .Returns((Category?)null);

            var budgetRepository = Substitute.For<IBudgetRepository>();

            var handler = new CreateBudgetCommandHandler(categoryRepository, budgetRepository);
            var command = new CreateBudgetCommand(CategoryId.New(), 2026, 8, 500m, "USD");

            var result = await handler.Handle(command, CancellationToken.None);

            result.IsFailure.Should().BeTrue();
            result.Error.Type.Should().Be(ErrorType.NotFound);
            await budgetRepository.DidNotReceive().AddAsync(Arg.Any<Budget>(), Arg.Any<CancellationToken>());
        }

        [Fact]
        public async Task Handle_WithExistingBudgetForCategoryAndPeriod_ReturnsConflict()
        {
            var category = Category.Create("Groceries", parentCategoryId: null);
            var period = BudgetPeriod.Create(2026, 8);
            var existingBudget = Budget.Create(category.Id, period, Money.Create(300m, "USD"));

            var categoryRepository = Substitute.For<ICategoryRepository>();
            categoryRepository.GetByIdAsync(category.Id, Arg.Any<CancellationToken>()).Returns(category);

            var budgetRepository = Substitute.For<IBudgetRepository>();
            budgetRepository
                .GetByCategoryAndPeriodAsync(category.Id, period, Arg.Any<CancellationToken>())
                .Returns(existingBudget);

            var handler = new CreateBudgetCommandHandler(categoryRepository, budgetRepository);
            var command = new CreateBudgetCommand(category.Id, 2026, 8, 500m, "USD");

            var result = await handler.Handle(command, CancellationToken.None);

            result.IsFailure.Should().BeTrue();
            result.Error.Type.Should().Be(ErrorType.Conflict);
            await budgetRepository.DidNotReceive().AddAsync(Arg.Any<Budget>(), Arg.Any<CancellationToken>());
        }
    }
}
