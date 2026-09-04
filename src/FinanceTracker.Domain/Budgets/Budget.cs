using FinanceTracker.Domain.Common;

namespace FinanceTracker.Domain.Budgets
{
    /// <summary>
    /// A spending limit for one Category in one BudgetPeriod. "One budget per
    /// category per period" is a cross-instance uniqueness rule enforced by
    /// the Application layer via the repository, not a Domain invariant.
    /// </summary>
    public sealed class Budget
    {
        public BudgetId Id { get; }

        public CategoryId CategoryId { get; }

        public BudgetPeriod Period { get; private set; }

        public Money LimitAmount { get; private set; }

        private Budget(BudgetId id, CategoryId categoryId, BudgetPeriod period, Money limitAmount)
        {
            Id = id;
            CategoryId = categoryId;
            Period = period;
            LimitAmount = limitAmount;
        }

        // Used only by EF Core to materialize a Budget loaded from the
        // database. Period and LimitAmount are mapped as EF Core complex
        // properties (see docs/adr/0003-ef-core-persistence-mapping.md), and
        // EF Core's constructor binding can never pass a complex-typed value
        // into a constructor parameter -- it can only set one via a property
        // afterward. This constructor exists purely so a constructor EF Core
        // CAN use (binding only Id and CategoryId) is available; Domain code
        // itself only ever calls the four-parameter constructor above.
        private Budget(BudgetId id, CategoryId categoryId)
        {
            Id = id;
            CategoryId = categoryId;
            Period = null!;
            LimitAmount = null!;
        }

        public static Budget Create(CategoryId categoryId, BudgetPeriod period, Money limitAmount)
        {
            ValidateLimit(limitAmount);
            return new Budget(BudgetId.New(), categoryId, period, limitAmount);
        }

        public void UpdateLimit(Money limitAmount)
        {
            ValidateLimit(limitAmount);
            LimitAmount = limitAmount;
        }

        private static void ValidateLimit(Money limitAmount)
        {
            if (limitAmount.Amount <= 0m)
                throw new ArgumentException("Budget limit must be greater than zero.", nameof(limitAmount));
        }
    }
}
