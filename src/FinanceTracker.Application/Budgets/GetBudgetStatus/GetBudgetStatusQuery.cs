using FinanceTracker.Application.Common;
using FinanceTracker.Domain.Common;
using MediatR;

namespace FinanceTracker.Application.Budgets.GetBudgetStatus
{
    /// <summary>Reports actual spending against one Budget's limit for its own Category and Period.</summary>
    public sealed record GetBudgetStatusQuery(BudgetId BudgetId) : IRequest<Result<BudgetStatusDto>>;
}
