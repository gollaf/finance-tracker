using FinanceTracker.Domain.Common;

namespace FinanceTracker.Domain.Accounts
{
    /// <summary>
    /// A place money is tracked. Deliberately holds no Balance — see
    /// docs/adr/0002-transaction-separate-aggregate-no-stored-balance.md.
    /// GetAccountBalance computes balance from Transactions instead.
    /// </summary>
    public sealed class Account
    {
        public const int MaxNameLength = 100;

        public AccountId Id { get; }

        public string Name { get; private set; }

        public AccountType Type { get; }

        public string Currency { get; }

        public bool IsClosed { get; private set; }

        private Account(AccountId id, string name, AccountType type, string currency)
        {
            Id = id;
            Name = name;
            Type = type;
            Currency = currency;
            IsClosed = false;
        }

        public static Account Create(string name, AccountType type, string currency)
        {
            ValidateName(name);
            CurrencyCodeValidator.EnsureValid(currency, nameof(currency));

            return new Account(AccountId.New(), name.Trim(), type, currency);
        }

        public void Rename(string name)
        {
            EnsureNotClosed();
            ValidateName(name);
            Name = name.Trim();
        }

        public void Close()
        {
            if (IsClosed)
                throw new InvalidOperationException("Account is already closed.");

            IsClosed = true;
        }

        private void EnsureNotClosed()
        {
            if (IsClosed)
                throw new InvalidOperationException("Cannot modify a closed account.");
        }

        private static void ValidateName(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
                throw new ArgumentException("Account name is required.", nameof(name));

            if (name.Trim().Length > MaxNameLength)
            {
                throw new ArgumentException(
                    $"Account name cannot exceed {MaxNameLength} characters.", nameof(name));
            }
        }
    }
}
