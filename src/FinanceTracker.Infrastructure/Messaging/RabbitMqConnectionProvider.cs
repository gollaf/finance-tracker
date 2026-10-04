using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RabbitMQ.Client;
using RabbitMQ.Client.Exceptions;

namespace FinanceTracker.Infrastructure.Messaging
{
    /// <summary>
    /// Owns this process's single connection to RabbitMQ; channels are
    /// opened on top of it.
    /// </summary>
    /// <remarks>
    /// The client library does not retry an initial connection, so
    /// GetConnectionAsync retries until it succeeds or the process stops. A
    /// connection lost later is restored by the client's automatic recovery.
    /// </remarks>
    public sealed class RabbitMqConnectionProvider : IAsyncDisposable
    {
        private static readonly TimeSpan RetryDelay = TimeSpan.FromSeconds(5);

        private readonly RabbitMqOptions _options;
        private readonly ILogger<RabbitMqConnectionProvider> _logger;
        private readonly SemaphoreSlim _connectLock = new(1, 1);

        private IConnection? _connection;

        public RabbitMqConnectionProvider(IOptions<RabbitMqOptions> options, ILogger<RabbitMqConnectionProvider> logger)
        {
            _options = options.Value;
            _logger = logger;
        }

        public async Task<IConnection> GetConnectionAsync(CancellationToken cancellationToken = default)
        {
            if (_connection is not null)
                return _connection;

            // Several hosted services ask at startup at the same moment; the
            // lock makes sure only one of them connects.
            await _connectLock.WaitAsync(cancellationToken);
            try
            {
                if (_connection is not null)
                    return _connection;

                var factory = new ConnectionFactory
                {
                    HostName = _options.Host,
                    Port = _options.Port,
                    UserName = _options.UserName,
                    Password = _options.Password,
                    VirtualHost = _options.VirtualHost,
                    // Identifies the process in the management UI.
                    ClientProvidedName = $"finance-tracker ({AppDomain.CurrentDomain.FriendlyName})",
                    AutomaticRecoveryEnabled = true,
                    TopologyRecoveryEnabled = true,
                };

                while (true)
                {
                    try
                    {
                        _connection = await factory.CreateConnectionAsync(cancellationToken);
                        _logger.LogInformation(
                            "Connected to RabbitMQ at {Host}:{Port}.", _options.Host, _options.Port);
                        return _connection;
                    }
                    catch (BrokerUnreachableException ex)
                    {
                        _logger.LogWarning(
                            ex,
                            "RabbitMQ at {Host}:{Port} is unreachable; retrying in {RetryDelay}.",
                            _options.Host, _options.Port, RetryDelay);
                        await Task.Delay(RetryDelay, cancellationToken);
                    }
                }
            }
            finally
            {
                _connectLock.Release();
            }
        }

        public async ValueTask DisposeAsync()
        {
            if (_connection is not null)
            {
                await _connection.CloseAsync();
                await _connection.DisposeAsync();
            }

            _connectLock.Dispose();
        }
    }
}
