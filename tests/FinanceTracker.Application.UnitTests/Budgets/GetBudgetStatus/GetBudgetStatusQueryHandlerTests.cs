using FinanceTracker.Application.Budgets;
using FinanceTracker.Application.Budgets.GetBudgetStatus;
using FinanceTracker.Application.Common;
using FinanceTracker.Application.Transactions;
using FinanceTracker.Domain.Budgets;
using FinanceTracker.Domain.Common;
using FinanceTracker.Domain.Transactions;
using FluentAssertions;
using NSubstitute;

namespace FinanceTracker.Application.UnitTests.Budgets.GetBudgetStatus
{
    public class GetBudgetStatusQueryHandlerTests
    {
        [Fact]
        public async Task Handle_WithSpendingUnderLimit_ReturnsPositiveRemainingAndNotOverBudget()
        {
            var categoryId = CategoryId.New();
            var period = BudgetPeriod.Create(2026, 6);
            var budget = Budget.Create(categoryId, period, Money.Create(200m, "USD"));

            var transactions = new[]
            {
                Transaction.Create(
                    AccountId.New(), Money.Create(50m, "USD"), TransactionType.Expense, "Store",
                    new DateOnly(2026, 6, 5), categoryId),
                Transaction.Create(
                    AccountId.New(), Money.Create(30m, "USD"), TransactionType.Expense, "Market",
                    new DateOnly(2026, 6, 10), categoryId),
                Transaction.Create(
                    AccountId.New(), Money.Create(500m, "USD"), TransactionType.Income, "Refund",
                    new DateOnly(2026, 6, 1), categoryId),
                Transaction.Create(
                    AccountId.New(), Money.Create(999m, "USD"), TransactionType.Expense, "Outside period",
                    new DateOnly(2026, 5, 1), categoryId),
            };

            var budgetRepository = Substitute.For<IBudgetRepository>();
            budgetRepository.GetByIdAsync(budget.Id, Arg.Any<CancellationToken>()).Returns(budget);

            var transactionRepository = Substitute.For<ITransactionRepository>();
            transactionRepository
                .GetByCategoryIdAsync(categoryId, Arg.Any<CancellationToken>())
                .Returns(transactions);

            var handler = new GetBudgetStatusQueryHandler(budgetRepository, transactionRepository);
            var query = new GetBudgetStatusQuery(budget.Id);

            var result = await handler.Handle(query, CancellationToken.None);

            result.IsSuccess.Should().BeTrue();
            result.Value.ActualSpending.Should().Be(Money.Create(80m, "USD"));
            result.Value.Remaining.Should().Be(Money.Create(120m, "USD"));
            result.Value.IsOverBudget.Should().BeFalse();
        }

        [Fact]
        public async Task Handle_WithSpendingOverLimit_ReturnsNegativeRemainingAndIsOverBudget()
        {
            var categoryId = CategoryId.New();
            var period = BudgetPeriod.Create(2026, 6);
            var budget = Budget.Create(categoryId, period, Money.Create(100m, "USD"));

            var transactions = new[]
            {
                Transaction.Create(
                    AccountId.New(), Money.Create(150m, "USD"), TransactionType.Expense, "Store",
                    new DateOnly(2026, 6, 5), categoryId),
            };

            var budgetRepository = Substitute.For<IBudgetRepository>();
            budgetRepository.GetByIdAsync(budget.Id, Arg.Any<CancellationToken>()).Returns(budget);

            var transactionRepository = Substitute.For<ITransactionRepository>();
            transactionRepository
                .GetByCategoryIdAsync(categoryId, Arg.Any<CancellationToken>())
                .Returns(transactions);

            var handler = new GetBudgetStatusQueryHandler(budgetRepository, transactionRepository);
            var query = new GetBudgetStatusQuery(budget.Id);

            var result = await handler.Handle(query, CancellationToken.None);

            result.IsSuccess.Should().BeTrue();
            result.Value.ActualSpending.Should().Be(Money.Create(150m, "USD"));
            result.Value.Remaining.Should().Be(Money.Create(-50m, "USD"));
            result.Value.IsOverBudget.Should().BeTrue();
        }

        [Fact]
        public async Task Handle_WithUnknownBudget_ReturnsNotFound()
        {
            var budgetRepository = Substitute.For<IBudgetRepository>();
            budgetRepository.GetByIdAsync(Arg.Any<BudgetId>(), Arg.Any<CancellationToken>()).Returns((Budget?)null);

            var transactionRepository = Substitute.For<ITransactionRepository>();

            var handler = new GetBudgetStatusQueryHandler(budgetRepository, transactionRepository);
            var query = new GetBudgetStatusQuery(BudgetId.New());

            var result = await handler.Handle(query, CancellationToken.None);

            result.IsFailure.Should().BeTrue();
            result.Error.Type.Should().Be(ErrorType.NotFound);
        }
    }
}
