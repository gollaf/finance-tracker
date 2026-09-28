using FinanceTracker.Application.Accounts.CreateAccount;
using FinanceTracker.Application.Categories.CreateCategory;
using FinanceTracker.Application.Transactions.AddTransaction;
using FinanceTracker.Domain.Accounts;
using FinanceTracker.Domain.Transactions;
using FluentAssertions;

namespace FinanceTracker.Worker.IntegrationTests
{
    /// <summary>
    /// The Worker split into its two roles, the way a scaled deployment runs
    /// it: one process with only the outbox relay, others with only the
    /// consumers (docs/adr/0018-worker-roles.md).
    ///
    /// The worst case for that split is the relay publishing while no
    /// consumer is running anywhere -- which is exactly how this test starts.
    /// If the queues only came into existence when a consumer started, the
    /// relay's publishes would be returned as unroutable and the outbox rows
    /// would never count as published.
    /// </summary>
    public sealed class SplitRolesPipelineTests : WorkerPipelineTestBase
    {
        protected override IReadOnlyDictionary<string, string?> WorkerSettings { get; } =
            new Dictionary<string, string?>
            {
                [WorkerServiceCollectionExtensions.RunConsumersKey] = "false",
            };

        [Fact]
        public async Task EventPublishedWhileNoConsumerRuns_IsHandledOnceAConsumerOnlyWorkerStarts()
        {
            var accountId = await SendAsync(new CreateAccountCommand("Checking", AccountType.Checking, "USD"));
            var transportId = await SendAsync(new CreateCategoryCommand("Transport"));

            var transactionId = await SendAsync(
                new AddTransactionCommand(accountId, 18m, TransactionType.Expense, "UBER *TRIP", Today));

            // The relay-only Worker publishes the TransactionAdded event. It
            // can only count as published if its queue already exists.
            await WaitUntilOutboxIsPublishedAsync();

            (await LoadCategoryAsync(transactionId)).Should().BeNull("no consumer has run yet");
            Suggester.Calls.Should().Be(0);

            // Now a second Worker with only the consumers -- another pod,
            // started later. The event has been waiting in its queue.
            await StartAdditionalWorkerAsync(new Dictionary<string, string?>
            {
                [WorkerServiceCollectionExtensions.RunOutboxRelayKey] = "false",
            });

            (await WaitForCategoryAsync(transactionId)).Should().Be(transportId);
            Suggester.Calls.Should().Be(1);
        }
    }
}
