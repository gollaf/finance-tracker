using FinanceTracker.Application.Categories;
using FinanceTracker.Application.Common;
using FinanceTracker.Domain.Budgets;
using FinanceTracker.Domain.Common;
using MediatR;

namespace FinanceTracker.Application.Budgets.CreateBudget
{
    /// <summary>
    /// Enforces the one-budget-per-category-per-period rule that Budget
    /// itself can't (see the remarks on Budget) — a second Create for the
    /// same Category and BudgetPeriod is a Conflict, not a duplicate row.
    /// </summary>
    public sealed class CreateBudgetCommandHandler : IRequestHandler<CreateBudgetCommand, Result<BudgetId>>
    {
        private readonly ICategoryRepository _categoryRepository;
        private readonly IBudgetRepository _budgetRepository;

        public CreateBudgetCommandHandler(ICategoryRepository categoryRepository, IBudgetRepository budgetRepository)
        {
            _categoryRepository = categoryRepository;
            _budgetRepository = budgetRepository;
        }

        public async Task<Result<BudgetId>> Handle(CreateBudgetCommand request, CancellationToken cancellationToken)
        {
            var category = await _categoryRepository.GetByIdAsync(request.CategoryId, cancellationToken);

            if (category is null)
            {
                return Result.Failure<BudgetId>(Error.NotFound(
                    "Category.NotFound", $"No category found with id '{request.CategoryId}'."));
            }

            var period = BudgetPeriod.Create(request.Year, request.Month);

            var existingBudget = await _budgetRepository.GetByCategoryAndPeriodAsync(
                request.CategoryId, period, cancellationToken);

            if (existingBudget is not null)
            {
                return Result.Failure<BudgetId>(Error.Conflict(
                    "Budget.AlreadyExists",
                    $"A budget already exists for category '{request.CategoryId}' in period '{period}'."));
            }

            var limit = Money.Create(request.LimitAmount, request.Currency);
            var budget = Budget.Create(request.CategoryId, period, limit);

            await _budgetRepository.AddAsync(budget, cancellationToken);

            return budget.Id;
        }
    }
}
