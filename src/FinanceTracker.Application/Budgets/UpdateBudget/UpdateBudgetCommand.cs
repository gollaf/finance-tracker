using FinanceTracker.Application.Common;
using FinanceTracker.Domain.Common;
using MediatR;

namespace FinanceTracker.Application.Budgets.UpdateBudget
{
    /// <summary>
    /// Updates a Budget's LimitAmount only — Category and Period are fixed at
    /// creation (Budget exposes no way to change either, see Budget.cs).
    /// Currency isn't accepted here; it's derived from the existing Budget's
    /// LimitAmount, same reasoning as UpdateTransaction deriving currency
    /// from the Transaction it's editing.
    /// </summary>
    public sealed record UpdateBudgetCommand(BudgetId BudgetId, decimal LimitAmount) : IRequest<Result>;
}
