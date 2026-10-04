using FinanceTracker.Application.Transactions.IntegrationEvents;
using FinanceTracker.Application.Transactions.SuggestCategory;
using FinanceTracker.Domain.Common;
using FinanceTracker.Infrastructure.Messaging;
using MediatR;

namespace FinanceTracker.Worker.Consumers
{
    /// <summary>
    /// Reacts to every TransactionAdded event by sending
    /// SuggestCategoryForTransactionCommand -- the messaging counterpart of
    /// an Api controller, with no business logic of its own.
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
        /// Named after what the consumer does, not the event, so another
        /// consumer of "transaction.added" gets its own queue. Static so
        /// AddWorker can register it even where this consumer doesn't run.
        /// </summary>
        public static readonly ConsumerQueue Queue =
            new("finance-tracker.categorize-transaction", TransactionAdded.EventName);

        protected override string QueueName => Queue.Name;

        protected override string RoutingKey => Queue.RoutingKey;

        protected override int DeliveryLimit => Queue.DeliveryLimit;

        protected override async Task HandleAsync(
            TransactionAdded message, IServiceProvider services, CancellationToken cancellationToken)
        {
            var sender = services.GetRequiredService<ISender>();

            var result = await sender.Send(
                new SuggestCategoryForTransactionCommand(new TransactionId(message.TransactionId)),
                cancellationToken);

            // Expected situations, including the AI being unavailable, are
            // successful outcomes, so a failure means a malformed message:
            // throw so it ends up in the dead-letter queue, not acked silently.
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
