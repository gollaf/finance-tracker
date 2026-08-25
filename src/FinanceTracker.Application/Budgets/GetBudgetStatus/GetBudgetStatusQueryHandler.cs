using FinanceTracker.Application.Common;
using FinanceTracker.Application.Transactions;
using FinanceTracker.Domain.Common;
using FinanceTracker.Domain.Transactions;
using MediatR;

namespace FinanceTracker.Application.Budgets.GetBudgetStatus
{
    /// <summary>
    /// Sums spending across every Account that has Transactions in the
    /// Budget's Category — a Budget isn't scoped to one Account, so its
    /// status can't be either. See ITransactionRepository.GetByCategoryIdAsync.
    /// </summary>
    public sealed class GetBudgetStatusQueryHandler : IRequestHandler<GetBudgetStatusQuery, Result<BudgetStatusDto>>
    {
        private readonly IBudgetRepository _budgetRepository;
        private readonly ITransactionRepository _transactionRepository;

        public GetBudgetStatusQueryHandler(
            IBudgetRepository budgetRepository, ITransactionRepository transactionRepository)
        {
            _budgetRepository = budgetRepository;
            _transactionRepository = transactionRepository;
        }

        public async Task<Result<BudgetStatusDto>> Handle(GetBudgetStatusQuery request, CancellationToken cancellationToken)
        {
            var budget = await _budgetRepository.GetByIdAsync(request.BudgetId, cancellationToken);

            if (budget is null)
            {
                return Result.Failure<BudgetStatusDto>(Error.NotFound(
                    "Budget.NotFound", $"No budget found with id '{request.BudgetId}'."));
            }

            var transactions = await _transactionRepository.GetByCategoryIdAsync(budget.CategoryId, cancellationToken);

            var actualSpending = transactions
                .Where(t => t.Type == TransactionType.Expense)
                .Where(t => budget.Period.Contains(t.OccurredOn))
                .Aggregate(Money.Zero(budget.LimitAmount.Currency), (total, t) => total + t.Amount);

            var remaining = budget.LimitAmount - actualSpending;

            var status = new BudgetStatusDto(
                budget.Id,
                budget.CategoryId,
                budget.Period,
                budget.LimitAmount,
                actualSpending,
                remaining,
                remaining.IsNegative);

            return Result.Success(status);
        }
    }
}
