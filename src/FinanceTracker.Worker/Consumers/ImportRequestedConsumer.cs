using FinanceTracker.Application.Imports.IntegrationEvents;
using FinanceTracker.Application.Imports.ProcessImport;
using FinanceTracker.Domain.Common;
using FinanceTracker.Infrastructure.Messaging;
using MediatR;

namespace FinanceTracker.Worker.Consumers
{
    /// <summary>
    /// Processes an import job when its ImportRequested event arrives, by
    /// sending ProcessImportJobCommand.
    /// </summary>
    /// <remarks>
    /// Redelivery is safe: the command runs in one database transaction and
    /// skips a job that is no longer Pending. An exception has already
    /// rolled everything back, so letting it propagate (and retry) is correct.
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

        public static readonly ConsumerQueue Queue =
            new("finance-tracker.process-import", ImportRequested.EventName);

        protected override string QueueName => Queue.Name;

        protected override string RoutingKey => Queue.RoutingKey;

        protected override int DeliveryLimit => Queue.DeliveryLimit;

        protected override async Task HandleAsync(
            ImportRequested message, IServiceProvider services, CancellationToken cancellationToken)
        {
            var sender = services.GetRequiredService<ISender>();

            var result = await sender.Send(
                new ProcessImportJobCommand(new ImportJobId(message.ImportJobId)), cancellationToken);

            // Expected situations are successful outcomes, so a failure means
            // a malformed message: throw so it ends up in the dead-letter queue.
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
