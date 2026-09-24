using FinanceTracker.Application.Imports.StartImport;
using FinanceTracker.Domain.Common;
using FinanceTracker.Domain.Imports;
using FinanceTracker.Domain.Transactions;
using FluentAssertions;

namespace FinanceTracker.Application.UnitTests.Imports.StartImport
{
    public class StartImportCommandValidatorTests
    {
        private static readonly DateOnly Today = DateOnly.FromDateTime(DateTime.UtcNow);

        private readonly StartImportCommandValidator _validator = new();

        private static ImportJobRow[] Rows(int count) =>
            Enumerable.Range(2, count)
                .Select(n => new ImportJobRow(n, 1m, TransactionType.Expense, "Row", Today))
                .ToArray();

        [Fact]
        public void Validate_WithAccountAndRows_IsValid()
        {
            var command = new StartImportCommand(AccountId.New(), Rows(2), Array.Empty<ImportJobRowError>());

            _validator.Validate(command).IsValid.Should().BeTrue();
        }

        [Fact]
        public void Validate_WithDefaultAccountId_IsInvalid()
        {
            var command = new StartImportCommand(default, Rows(1), Array.Empty<ImportJobRowError>());

            _validator.Validate(command).IsValid.Should().BeFalse();
        }

        [Fact]
        public void Validate_WithNoRows_IsInvalid()
        {
            var command = new StartImportCommand(AccountId.New(), Rows(0), Array.Empty<ImportJobRowError>());

            _validator.Validate(command).IsValid.Should().BeFalse();
        }

        [Fact]
        public void Validate_WithMoreThanMaxRows_IsInvalid()
        {
            var command = new StartImportCommand(AccountId.New(), Rows(ImportJob.MaxRows + 1), Array.Empty<ImportJobRowError>());

            _validator.Validate(command).IsValid.Should().BeFalse();
        }
    }
}
