using FinanceTracker.Application.Accounts.CreateAccount;
using FinanceTracker.Application.Categories.CreateCategory;
using FinanceTracker.Application.Imports.StartImport;
using FinanceTracker.Domain.Accounts;
using FinanceTracker.Domain.Imports;
using FinanceTracker.Domain.Transactions;
using FluentAssertions;

namespace FinanceTracker.Worker.IntegrationTests
{
    /// <summary>
    /// The asynchronous import pipeline, end to end:
    ///
    ///   StartImport (Pending job + ImportRequested, one commit)
    ///     -> OutboxRelay -> RabbitMQ -> ImportRequestedConsumer
    ///     -> ProcessImportJobCommand, in one database transaction:
    ///        every row's Transaction + TransactionAdded, and the job's outcome
    ///     -> and then each imported Transaction's own categorization pipeline
    /// </summary>
    public sealed class ImportPipelineTests : WorkerPipelineTestBase
    {
        [Fact]
        public async Task StartedImport_IsProcessedByTheWorker_AndTheJobReportsTheOutcome()
        {
            var accountId = await SendAsync(new CreateAccountCommand("Checking", AccountType.Checking, "USD"));
            await SendAsync(new CreateCategoryCommand("Groceries"));
            var transportId = await SendAsync(new CreateCategoryCommand("Transport"));

            var importJobId = await SendAsync(new StartImportCommand(
                accountId,
                new[]
                {
                    new ImportJobRow(2, 18m, TransactionType.Expense, "UBER *TRIP", Today),
                    // Parses fine, but a blank description fails Transaction.Create.
                    new ImportJobRow(3, 5m, TransactionType.Expense, "   ", Today),
                    new ImportJobRow(4, 2500m, TransactionType.Income, "SALARY", Today),
                },
                // As if line 5 of the file had already failed to parse.
                new[] { new ImportJobRowError(5, "Invalid amount: 'abc'.") }));

            var job = await WaitForProcessedImportJobAsync(importJobId);

            job.Status.Should().Be(ImportJobStatus.Completed);
            job.ImportedCount.Should().Be(2);
            job.Errors.Select(e => e.RowNumber).Should().Equal(3, 5);

            var transactions = await LoadTransactionsAsync(accountId);
            transactions.Select(t => t.Description).Should().BeEquivalentTo("UBER *TRIP", "SALARY");

            // Imported transactions then go through categorization like any
            // other new transaction.
            var uber = transactions.Single(t => t.Description == "UBER *TRIP");
            (await WaitForCategoryAsync(uber.Id)).Should().Be(transportId);
        }
    }
}
