using FinanceTracker.Domain.Common;
using FinanceTracker.Domain.Imports;
using FinanceTracker.Domain.Transactions;
using FluentAssertions;

namespace FinanceTracker.Domain.UnitTests.Imports
{
    public class ImportJobTests
    {
        private static readonly DateOnly Today = DateOnly.FromDateTime(DateTime.UtcNow);

        private static ImportJobRow Row(int rowNumber, string description = "Coffee") =>
            new(rowNumber, 4.5m, TransactionType.Expense, description, Today);

        private static ImportJob NewJob(params ImportJobRowError[] parseErrors) =>
            ImportJob.Create(AccountId.New(), new[] { Row(2), Row(4) }, parseErrors);

        [Fact]
        public void Create_StartsPendingWithRowsAndParseErrors()
        {
            var accountId = AccountId.New();

            var job = ImportJob.Create(
                accountId, new[] { Row(2), Row(4) }, new[] { new ImportJobRowError(3, "Bad amount") });

            job.Status.Should().Be(ImportJobStatus.Pending);
            job.IsPending.Should().BeTrue();
            job.AccountId.Should().Be(accountId);
            job.Rows.Select(r => r.RowNumber).Should().Equal(2, 4);
            job.Errors.Should().ContainSingle().Which.RowNumber.Should().Be(3);
            job.ImportedCount.Should().Be(0);
            job.CompletedAt.Should().BeNull();
        }

        [Fact]
        public void Create_WithNoRows_Throws()
        {
            var act = () => ImportJob.Create(AccountId.New(), Array.Empty<ImportJobRow>(), Array.Empty<ImportJobRowError>());

            act.Should().Throw<ArgumentException>();
        }

        [Fact]
        public void Create_WithMoreThanMaxRows_Throws()
        {
            var rows = Enumerable.Range(2, ImportJob.MaxRows + 1).Select(n => Row(n));

            var act = () => ImportJob.Create(AccountId.New(), rows, Array.Empty<ImportJobRowError>());

            act.Should().Throw<ArgumentException>();
        }

        [Fact]
        public void Complete_RecordsCountAndMergesErrorsInLineOrder()
        {
            var job = NewJob(new ImportJobRowError(5, "Parse error"));

            job.Complete(1, new[] { new ImportJobRowError(4, "Description is required") });

            job.Status.Should().Be(ImportJobStatus.Completed);
            job.IsPending.Should().BeFalse();
            job.ImportedCount.Should().Be(1);
            job.Errors.Select(e => e.RowNumber).Should().Equal(4, 5);
            job.CompletedAt.Should().NotBeNull();
        }

        [Theory]
        [InlineData(-1)]
        [InlineData(3)]
        public void Complete_WithImpossibleImportedCount_Throws(int importedCount)
        {
            var job = NewJob();

            var act = () => job.Complete(importedCount, Array.Empty<ImportJobRowError>());

            act.Should().Throw<ArgumentOutOfRangeException>();
        }

        [Fact]
        public void Complete_WhenAlreadyCompleted_Throws()
        {
            var job = NewJob();
            job.Complete(2, Array.Empty<ImportJobRowError>());

            var act = () => job.Complete(2, Array.Empty<ImportJobRowError>());

            act.Should().Throw<InvalidOperationException>();
        }

        [Fact]
        public void Fail_RecordsReasonAndStopsBeingPending()
        {
            var job = NewJob();

            job.Fail("  Account was closed.  ");

            job.Status.Should().Be(ImportJobStatus.Failed);
            job.FailureReason.Should().Be("Account was closed.");
            job.ImportedCount.Should().Be(0);
            job.CompletedAt.Should().NotBeNull();
        }

        [Fact]
        public void Fail_WithBlankReason_Throws()
        {
            var job = NewJob();

            var act = () => job.Fail("   ");

            act.Should().Throw<ArgumentException>();
        }

        [Fact]
        public void Complete_AfterFail_Throws()
        {
            var job = NewJob();
            job.Fail("Account was closed.");

            var act = () => job.Complete(0, Array.Empty<ImportJobRowError>());

            act.Should().Throw<InvalidOperationException>();
        }
    }
}
