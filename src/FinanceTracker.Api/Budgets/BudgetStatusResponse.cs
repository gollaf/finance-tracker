namespace FinanceTracker.Api.Budgets
{
    /// <summary>
    /// Flattens BudgetStatusDto'''s domain types (BudgetPeriod, Money) into
    /// primitives for the wire, same reasoning as AccountBalanceResponse.
    /// LimitAmount, ActualSpending and Remaining always share one Currency --
    /// the handler only ever combines Money values in the Budget'''s own
    /// currency -- so a single Currency field covers all three.
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
