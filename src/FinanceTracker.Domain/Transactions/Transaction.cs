using FinanceTracker.Domain.Common;

namespace FinanceTracker.Domain.Transactions
{
    /// <summary>
    /// A single dated movement of money in or out of one Account. Its own
    /// aggregate root, referencing Account and Category by ID only — see
    /// docs/adr/0002-transaction-separate-aggregate-no-stored-balance.md.
    /// </summary>
    public sealed class Transaction
    {
        public const int MaxDescriptionLength = 500;

        public TransactionId Id { get; }

        public AccountId AccountId { get; }

        public CategoryId? CategoryId { get; private set; }

        public Money Amount { get; private set; }

        public TransactionType Type { get; private set; }

        public string Description { get; private set; }

        public DateOnly OccurredOn { get; private set; }

        public DateTimeOffset CreatedAt { get; }

        private Transaction(
            TransactionId id,
            AccountId accountId,
            Money amount,
            TransactionType type,
            string description,
            DateOnly occurredOn,
            CategoryId? categoryId,
            DateTimeOffset createdAt)
        {
            Id = id;
            AccountId = accountId;
            Amount = amount;
            Type = type;
            Description = description;
            OccurredOn = occurredOn;
            CategoryId = categoryId;
            CreatedAt = createdAt;
        }

        // Used only by EF Core to materialize a Transaction loaded from
        // the database. Amount is mapped as an EF Core complex property
        // (see docs/adr/0003-ef-core-persistence-mapping.md and its
        // amendment), and EF Core's constructor binding can never pass a
        // complex-typed value into a constructor parameter -- only the
        // scalar/converted ones. This constructor exists purely so a
        // constructor EF Core CAN use (binding everything except Amount)
        // is available; Domain code itself only ever calls the
        // eight-parameter constructor above, which then sets Amount too.
        private Transaction(
            TransactionId id,
            AccountId accountId,
            TransactionType type,
            string description,
            DateOnly occurredOn,
            CategoryId? categoryId,
            DateTimeOffset createdAt)
        {
            Id = id;
            AccountId = accountId;
            Type = type;
            Description = description;
            OccurredOn = occurredOn;
            CategoryId = categoryId;
            CreatedAt = createdAt;
            Amount = null!;
        }

        public static Transaction Create(
            AccountId accountId,
            Money amount,
            TransactionType type,
            string description,
            DateOnly occurredOn,
            CategoryId? categoryId = null,
            DateTimeOffset? createdAt = null)
        {
            var effectiveCreatedAt = createdAt ?? DateTimeOffset.UtcNow;

            ValidateAmount(amount);
            ValidateDescription(description);
            ValidateOccurredOn(occurredOn, effectiveCreatedAt);

            return new Transaction(
                TransactionId.New(),
                accountId,
                amount,
                type,
                description.Trim(),
                occurredOn,
                categoryId,
                effectiveCreatedAt);
        }

        /// <summary>
        /// Amount with direction applied: positive for Income, negative for
        /// Expense. Derived on demand, never stored — used by balance and
        /// spending-summary queries.
        /// </summary>
        public Money SignedAmount => Type == TransactionType.Expense ? Amount.Negate() : Amount;

        public void Recategorize(CategoryId? categoryId) => CategoryId = categoryId;

        public void UpdateAmount(Money amount)
        {
            ValidateAmount(amount);
            Amount = amount;
        }

        public void UpdateDescription(string description)
        {
            ValidateDescription(description);
            Description = description.Trim();
        }

        public void UpdateOccurredOn(DateOnly occurredOn)
        {
            ValidateOccurredOn(occurredOn, DateTimeOffset.UtcNow);
            OccurredOn = occurredOn;
        }

        private static void ValidateAmount(Money amount)
        {
            if (amount.Amount <= 0m)
                throw new ArgumentException("Transaction amount must be greater than zero.", nameof(amount));
        }

        private static void ValidateDescription(string description)
        {
            if (string.IsNullOrWhiteSpace(description))
                throw new ArgumentException("Transaction description is required.", nameof(description));

            if (description.Trim().Length > MaxDescriptionLength)
            {
                throw new ArgumentException(
                    $"Transaction description cannot exceed {MaxDescriptionLength} characters.",
                    nameof(description));
            }
        }

        private static void ValidateOccurredOn(DateOnly occurredOn, DateTimeOffset asOf)
        {
            if (occurredOn > DateOnly.FromDateTime(asOf.UtcDateTime))
                throw new ArgumentException("Transaction date cannot be in the future.", nameof(occurredOn));
        }
    }
}
