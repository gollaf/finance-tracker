namespace FinanceTracker.Domain.Common
{
    /// <summary>
    /// Shared ISO 4217 currency code validation, used by every Domain type that
    /// carries a currency (currently <see cref="Money"/> and Account).
    /// </summary>
    internal static class CurrencyCodeValidator
    {
        public static void EnsureValid(string? currency, string paramName)
        {
            if (string.IsNullOrWhiteSpace(currency))
                throw new ArgumentException("Currency is required.", paramName);

            if (currency.Length != 3 || !IsUpperCaseLetters(currency))
                throw new ArgumentException(
                    "Currency must be a 3-letter uppercase ISO 4217 code (e.g. \"USD\").",
                    paramName);
        }

        private static bool IsUpperCaseLetters(string value)
        {
            foreach (var c in value)
            {
                if (c is < 'A' or > 'Z')
                    return false;
            }

            return true;
        }
    }
}
