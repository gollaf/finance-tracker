using FinanceTracker.Application.Accounts.CreateAccount;
using FinanceTracker.Application.Categories.CreateCategory;
using FinanceTracker.Application.Categorization.CreateCategorizationRule;
using FinanceTracker.Application.Imports.StartImport;
using FinanceTracker.Application.Transactions.AddTransaction;
using FinanceTracker.Domain.Accounts;
using FinanceTracker.Domain.Common;
using FinanceTracker.Domain.Imports;
using FinanceTracker.Domain.Transactions;
using FluentAssertions;

namespace FinanceTracker.Worker.IntegrationTests
{
    /// <summary>
    /// The asynchronous categorization pipeline, end to end:
    ///
    ///   AddTransaction (or an import)
    ///     -> outbox row (same DB transaction)
    ///     -> OutboxRelay publishes it
    ///     -> RabbitMQ routes it to the categorize queue
    ///     -> TransactionAddedCategorizationConsumer
    ///     -> SuggestCategoryForTransactionCommand
    ///     -> conditional update in Postgres
    /// </summary>
    public sealed class CategorizationPipelineTests : WorkerPipelineTestBase
    {
        [Fact]
        public async Task AddedTransaction_IsCategorizedByTheWorker()
        {
            var accountId = await SendAsync(new CreateAccountCommand(Owner, "Checking", AccountType.Checking, "USD"));
            await SendAsync(new CreateCategoryCommand(Owner, "Groceries"));
            var transportId = await SendAsync(new CreateCategoryCommand(Owner, "Transport"));

            var transactionId = await SendAsync(
                new AddTransactionCommand(Owner, accountId, 18m, TransactionType.Expense, "UBER *TRIP", Today));

            var categoryId = await WaitForCategoryAsync(transactionId);

            categoryId.Should().Be(transportId);
            Suggester.Calls.Should().Be(1);
        }

        [Fact]
        public async Task TransactionCategorizedByARule_KeepsItsCategory_AndTheAiIsNeverAsked()
        {
            var accountId = await SendAsync(new CreateAccountCommand(Owner, "Checking", AccountType.Checking, "USD"));
            var groceriesId = await SendAsync(new CreateCategoryCommand(Owner, "Groceries"));
            var transportId = await SendAsync(new CreateCategoryCommand(Owner, "Transport"));
            await SendAsync(new CreateCategorizationRuleCommand(Owner, "supermarket", groceriesId, Priority: 1));

            // Imported through the real import pipeline, so the rule is
            // applied during processing, in the Worker.
            var importJobId = await SendAsync(new StartImportCommand(
                Owner, accountId,
                new[] { new ImportJobRow(2, 54m, TransactionType.Expense, "CITY SUPERMARKET", Today) },
                Array.Empty<ImportJobRowError>()));
            await WaitForProcessedImportJobAsync(importJobId);
            var ruleCategorized = (await LoadTransactionsAsync(accountId)).Single();

            // A second, uncategorized transaction, added only now. Its
            // TransactionAdded event is younger than the imported one's, and
            // the relay publishes oldest first into the same FIFO queue with
            // one consumer and prefetch 1 -- so once this one has been
            // categorized, the imported one's event has certainly been handled.
            var uncategorizedId = await SendAsync(
                new AddTransactionCommand(Owner, accountId, 18m, TransactionType.Expense, "UBER *TRIP", Today));
            (await WaitForCategoryAsync(uncategorizedId)).Should().Be(transportId);

            (await LoadCategoryAsync(ruleCategorized.Id)).Should().Be(groceriesId);
            Suggester.Calls.Should().Be(1, "only the uncategorized transaction should have reached the AI");
        }

        [Fact]
        public async Task TheAiIsOfferedOnlyTheTransactionOwnersCategories()
        {
            // The stub always picks "Transport". Only Owner has a category
            // with that name; the stranger has just "Groceries".
            var stranger = new UserId(Guid.NewGuid());
            var strangersAccountId = await SendAsync(
                new CreateAccountCommand(stranger, "Checking", AccountType.Checking, "USD"));
            await SendAsync(new CreateCategoryCommand(stranger, "Groceries"));

            var accountId = await SendAsync(new CreateAccountCommand(Owner, "Checking", AccountType.Checking, "USD"));
            var transportId = await SendAsync(new CreateCategoryCommand(Owner, "Transport"));

            await SendAsync(
                new AddTransactionCommand(stranger, strangersAccountId, 18m, TransactionType.Expense, "UBER *TRIP", Today));

            // Same FIFO reasoning as above: once Owner's younger transaction
            // has been categorized, the stranger's has been handled too.
            var ownersTransactionId = await SendAsync(
                new AddTransactionCommand(Owner, accountId, 18m, TransactionType.Expense, "UBER *TRIP", Today));
            (await WaitForCategoryAsync(ownersTransactionId)).Should().Be(transportId);

            var strangersTransaction = (await LoadTransactionsAsync(stranger, strangersAccountId)).Single();
            strangersTransaction.CategoryId.Should().BeNull(
                "Owner's Transport category must never be offered for, or assigned to, the stranger's transaction");
            Suggester.Calls.Should().Be(2);
        }
    }
}
