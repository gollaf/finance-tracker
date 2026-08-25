using FinanceTracker.Application.Common;
using FinanceTracker.Domain.Common;
using MediatR;

namespace FinanceTracker.Application.Budgets.UpdateBudget
{
    public sealed class UpdateBudgetCommandHandler : IRequestHandler<UpdateBudgetCommand, Result>
    {
        private readonly IBudgetRepository _budgetRepository;

        public UpdateBudgetCommandHandler(IBudgetRepository budgetRepository)
        {
            _budgetRepository = budgetRepository;
        }

        public async Task<Result> Handle(UpdateBudgetCommand request, CancellationToken cancellationToken)
        {
            var budget = await _budgetRepository.GetByIdAsync(request.BudgetId, cancellationToken);

            if (budget is null)
            {
                return Result.Failure(Error.NotFound(
                    "Budget.NotFound", $"No budget found with id '{request.BudgetId}'."));
            }

            var newLimit = Money.Create(request.LimitAmount, budget.LimitAmount.Currency);
            budget.UpdateLimit(newLimit);

            await _budgetRepository.UpdateAsync(budget, cancellationToken);

            return Result.Success();
        }
    }
}
