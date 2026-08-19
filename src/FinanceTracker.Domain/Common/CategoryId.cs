namespace FinanceTracker.Domain.Common
{
    public readonly record struct CategoryId(Guid Value)
    {
        public static CategoryId New() => new(Guid.NewGuid());

        public override string ToString() => Value.ToString();
    }
}
