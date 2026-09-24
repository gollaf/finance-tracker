namespace FinanceTracker.Domain.Common
{
    public readonly record struct ImportJobId(Guid Value)
    {
        public static ImportJobId New() => new(Guid.NewGuid());

        public override string ToString() => Value.ToString();
    }
}
