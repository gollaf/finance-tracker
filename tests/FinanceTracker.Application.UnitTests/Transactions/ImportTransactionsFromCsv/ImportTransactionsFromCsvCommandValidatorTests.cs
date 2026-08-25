using FinanceTracker.Application.Transactions.ImportTransactionsFromCsv;
using FinanceTracker.Domain.Common;
using FinanceTracker.Domain.Transactions;
using FluentAssertions;

namespace FinanceTracker.Application.UnitTests.Transactions.ImportTransactionsFromCsv
{
    public class ImportTransactionsFromCsvCommandValidatorTests
    {
        private readonly ImportTransactionsFromCsvCommandValidator _validator = new();
        private static readonly DateOnly Today = DateOnly.FromDateTime(DateTime.UtcNow);

        private static ImportTransactionsFromCsvCommand ValidCommand() =>
            new(AccountId.New(), new[] { new CsvTransactionRow(20m, TransactionType.Expense, "Coffee", Today) });

        [Fact]
        public void Validate_WithValidCommand_IsValid()
        {
            _validator.Validate(ValidCommand()).IsValid.Should().BeTrue();
        }

        [Fact]
        public void Validate_WithDefaultAccountId_IsInvalid()
        {
            var result = _validator.Validate(ValidCommand() with { AccountId = default });

            result.IsValid.Should().BeFalse();
            result.Errors.Should().Contain(e => e.PropertyName == nameof(ImportTransactionsFromCsvCommand.AccountId));
        }

        [Fact]
        public void Validate_WithNoRows_IsInvalid()
        {
            var result = _validator.Validate(ValidCommand() with { Rows = Array.Empty<CsvTransactionRow>() });

            result.IsValid.Should().BeFalse();
            result.Errors.Should().Contain(e => e.PropertyName == nameof(ImportTransactionsFromCsvCommand.Rows));
        }

        [Fact]
        public void Validate_DoesNotInspectRowContent()
        {
            // A row with an empty description and a non-positive amount is
            // still structurally valid at the command level — content
            // validation is deliberately deferred to the handler so one bad
            // row can't sink the whole batch (see the validator's remarks).
            var invalidRow = new CsvTransactionRow(-5m, TransactionType.Expense, string.Empty, Today);

            var result = _validator.Validate(ValidCommand() with { Rows = new[] { invalidRow } });

            result.IsValid.Should().BeTrue();
        }
    }
}
