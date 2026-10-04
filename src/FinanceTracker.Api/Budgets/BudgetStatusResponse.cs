namespace FinanceTracker.Api.Budgets
{
    /// <summary>
    /// Flattens BudgetStatusDto into primitives. LimitAmount, ActualSpending
    /// and Remaining are all in the Budget's currency, so one Currency field
    /// covers all three.
    /// </summary>
    public sealed record BudgetStatusResponse(
        Guid BudgetId,
        Guid CategoryId,
        int Year,
        int Month,
        decimal LimitAmount,
        decimal ActualSpending,
        decimal Remaining,
        string Currency,
        bool IsOverBudget);
}
