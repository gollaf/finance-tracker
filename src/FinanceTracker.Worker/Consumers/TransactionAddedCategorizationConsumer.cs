using FinanceTracker.Application.Transactions.IntegrationEvents;
using FinanceTracker.Application.Transactions.SuggestCategory;
using FinanceTracker.Domain.Common;
using FinanceTracker.Infrastructure.Messaging;
using MediatR;

namespace FinanceTracker.Worker.Consumers
{
    /// <summary>
    /// Reacts to every TransactionAdded event by sending
    /// SuggestCategoryForTransactionCommand. A thin inbound adapter -- the
    /// messaging counterpart of an Api controller: it translates a message
    /// into a MediatR command and contains no business logic of its own.
    /// Acknowledging, retrying, dead-lettering, and the per-message DI
    /// scope all come from RabbitMqConsumer (see
    /// docs/adr/0012-rabbitmq-topology-and-delivery-guarantees.md).
    /// </summary>
    public sealed class TransactionAddedCategorizationConsumer : RabbitMqConsumer<TransactionAdded>
    {
        public TransactionAddedCategorizationConsumer(
            RabbitMqConnectionProvider connectionProvider,
            IServiceScopeFactory scopeFactory,
            ILogger<TransactionAddedCategorizationConsumer> logger)
            : base(connectionProvider, scopeFactory, logger)
        {
        }

        /// <summary>
        /// Named after what this consumer does, not after the event: another
        /// consumer interested in "transaction.added" (say, budget alerts)
        /// gets its own queue and its own copy of every event.
        /// </summary>
        protected override string QueueName => "finance-tracker.categorize-transaction";

        protected override string RoutingKey => TransactionAdded.EventName;

        protected override async Task HandleAsync(
            TransactionAdded message, IServiceProvider services, CancellationToken cancellationToken)
        {
            var sender = services.GetRequiredService<ISender>();

            var result = await sender.Send(
                new SuggestCategoryForTransactionCommand(new TransactionId(message.TransactionId)),
                cancellationToken);

            // The handler turns every expected situation -- including the AI
            // being unavailable -- into a successful outcome. A failure here
            // can only be a validation failure (an empty id): a malformed
            // message that retrying won't fix. Throwing lets the queue's
            // delivery limit move it to the dead-letter queue, where it is
            // visible, instead of acknowledging it away silently.
            if (result.IsFailure)
            {
                throw new InvalidOperationException(
                    $"SuggestCategoryForTransaction failed for {message.TransactionId}: " +
                    $"{result.Error.Code} {result.Error.Message}");
            }

            Logger.LogInformation(
                "Transaction {TransactionId}: {Outcome}.", message.TransactionId, result.Value);
        }
    }
}
