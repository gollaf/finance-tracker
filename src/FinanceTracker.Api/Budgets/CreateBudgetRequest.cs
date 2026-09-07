namespace FinanceTracker.Api.Budgets
{
    public sealed record CreateBudgetRequest(Guid CategoryId, int Year, int Month, decimal LimitAmount, string Currency);
}
