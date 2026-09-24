using FinanceTracker.Application.Imports.IntegrationEvents;
using FinanceTracker.Application.Imports.ProcessImport;
using FinanceTracker.Domain.Common;
using FinanceTracker.Infrastructure.Messaging;
using MediatR;

namespace FinanceTracker.Worker.Consumers
{
    /// <summary>
    /// Processes an import job when its ImportRequested event arrives, by
    /// sending ProcessImportJobCommand. Same thin-adapter shape as
    /// TransactionAddedCategorizationConsumer (see
    /// docs/adr/0015-integration-event-contracts.md).
    /// </summary>
    /// <remarks>
    /// Redelivery is safe without anything extra here: the command runs the
    /// whole import in one database transaction and skips a job that is no
    /// longer Pending (docs/adr/0016-asynchronous-csv-import.md). An
    /// exception from it -- the database going away mid-import, say -- has
    /// already rolled everything back by the time it reaches this class, so
    /// letting it propagate (and the message be retried) is correct.
    /// </remarks>
    public sealed class ImportRequestedConsumer : RabbitMqConsumer<ImportRequested>
    {
        public ImportRequestedConsumer(
            RabbitMqConnectionProvider connectionProvider,
            IServiceScopeFactory scopeFactory,
            ILogger<ImportRequestedConsumer> logger)
            : base(connectionProvider, scopeFactory, logger)
        {
        }

        protected override string QueueName => "finance-tracker.process-import";

        protected override string RoutingKey => ImportRequested.EventName;

        protected override async Task HandleAsync(
            ImportRequested message, IServiceProvider services, CancellationToken cancellationToken)
        {
            var sender = services.GetRequiredService<ISender>();

            var result = await sender.Send(
                new ProcessImportJobCommand(new ImportJobId(message.ImportJobId)), cancellationToken);

            // As with categorization: every expected situation is a
            // successful outcome, so a failure can only be a malformed
            // message (an empty id) -- dead-letter it where it's visible.
            if (result.IsFailure)
            {
                throw new InvalidOperationException(
                    $"ProcessImportJob failed for {message.ImportJobId}: " +
                    $"{result.Error.Code} {result.Error.Message}");
            }

            Logger.LogInformation(
                "Import job {ImportJobId}: {Outcome}.", message.ImportJobId, result.Value);
        }
    }
}
