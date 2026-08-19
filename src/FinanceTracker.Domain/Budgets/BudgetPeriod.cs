namespace FinanceTracker.Domain.Budgets
{
    /// <summary>One calendar month — the granularity a Budget is set at.</summary>
    public sealed record BudgetPeriod
    {
        public int Year { get; }

        public int Month { get; }

        private BudgetPeriod(int year, int month)
        {
            Year = year;
            Month = month;
        }

        public static BudgetPeriod Create(int year, int month)
        {
            if (month is < 1 or > 12)
                throw new ArgumentOutOfRangeException(nameof(month), month, "Month must be between 1 and 12.");

            if (year < 1)
                throw new ArgumentOutOfRangeException(nameof(year), year, "Year must be positive.");

            return new BudgetPeriod(year, month);
        }

        public bool Contains(DateOnly date) => date.Year == Year && date.Month == Month;

        public override string ToString() => $"{Year:D4}-{Month:D2}";
    }
}
