namespace FinanceTracker.Domain.Common
{
    public readonly record struct CategorizationRuleId(Guid Value)
    {
        public static CategorizationRuleId New() => new(Guid.NewGuid());

        public override string ToString() => Value.ToString();
    }
}
