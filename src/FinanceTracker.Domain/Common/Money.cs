namespace FinanceTracker.Domain.Common
{
    /// <summary>
    /// An immutable monetary amount in a specific currency. Amount can be
    /// negative or positive — direction for a Transaction is expressed via
    /// TransactionType, not the sign of Money. Arithmetic between two Money
    /// values requires matching currencies; this type never converts currency.
    /// </summary>
    public sealed record Money
    {
        public decimal Amount { get; }

        public string Currency { get; }

        private Money(decimal amount, string currency)
        {
            Amount = amount;
            Currency = currency;
        }

        public static Money Create(decimal amount, string currency)
        {
            CurrencyCodeValidator.EnsureValid(currency, nameof(currency));
            return new Money(amount, currency);
        }

        public static Money Zero(string currency) => Create(0m, currency);

        public bool IsZero => Amount == 0m;

        public bool IsPositive => Amount > 0m;

        public bool IsNegative => Amount < 0m;

        public Money Negate() => new(-Amount, Currency);

        public static Money operator +(Money left, Money right)
        {
            EnsureSameCurrency(left, right);
            return new Money(left.Amount + right.Amount, left.Currency);
        }

        public static Money operator -(Money left, Money right)
        {
            EnsureSameCurrency(left, right);
            return new Money(left.Amount - right.Amount, left.Currency);
        }

        public static Money operator -(Money money) => money.Negate();

        private static void EnsureSameCurrency(Money left, Money right)
        {
            if (left.Currency != right.Currency)
            {
                throw new InvalidOperationException(
                    $"Cannot combine amounts in different currencies: {left.Currency} and {right.Currency}.");
            }
        }

        public override string ToString() => $"{Amount} {Currency}";
    }
}
