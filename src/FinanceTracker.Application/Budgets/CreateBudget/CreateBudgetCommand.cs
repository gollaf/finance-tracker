using FinanceTracker.Application.Common;
using FinanceTracker.Domain.Common;
using MediatR;

namespace FinanceTracker.Application.Budgets.CreateBudget
{
    /// <summary>
    /// Currency is supplied directly here, not derived like AddTransaction
    /// derives it from an Account — a Budget targets a Category, not an
    /// Account, so there's nothing to derive it from.
    /// </summary>
    public sealed record CreateBudgetCommand(
        CategoryId CategoryId,
        int Year,
        int Month,
        decimal LimitAmount,
        string Currency) : IRequest<Result<BudgetId>>;
}
