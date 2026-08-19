namespace FinanceTracker.Domain.Common
{
    public readonly record struct BudgetId(Guid Value)
    {
        public static BudgetId New() => new(Guid.NewGuid());

        public override string ToString() => Value.ToString();
    }
}
