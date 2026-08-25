using FinanceTracker.Domain.Budgets;
using FinanceTracker.Domain.Common;

namespace FinanceTracker.Application.Budgets.GetBudgetStatus
{
    /// <summary>
    /// A Budget's limit alongside actual spending against it for its own
    /// Period. Remaining can be negative — that's what being over budget
    /// looks like, not an error state.
    /// </summary>
    public sealed record BudgetStatusDto(
        BudgetId BudgetId,
        CategoryId CategoryId,
        BudgetPeriod Period,
        Money LimitAmount,
        Money ActualSpending,
        Money Remaining,
        bool IsOverBudget);
}
