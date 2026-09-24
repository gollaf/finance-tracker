using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RabbitMQ.Client;
using RabbitMQ.Client.Exceptions;

namespace FinanceTracker.Infrastructure.Messaging
{
    /// <summary>
    /// Owns this process's single connection to RabbitMQ. One connection per
    /// process, many channels on top of it, is the pattern RabbitMQ
    /// recommends: a connection is a real TCP connection plus an AMQP
    /// handshake (expensive), a channel is a lightweight virtual connection
    /// multiplexed inside it (cheap). Registered as a Singleton for exactly
    /// that reason.
    /// </summary>
    /// <remarks>
    /// Two different kinds of "the broker isn't there" are handled in two
    /// different places:
    /// <list type="bullet">
    /// <item>Broker unreachable when this process starts: the client
    /// library does NOT retry an initial connection, so
    /// GetConnectionAsync retries it itself, every RetryDelay, until it
    /// succeeds or the process is stopping.</item>
    /// <item>Connection lost after it was established: the client's
    /// automatic recovery (on by default, enabled explicitly below for
    /// visibility) reconnects and re-creates channels, consumers, and
    /// declared topology on its own.</item>
    /// </list>
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

            // Several hosted services ask for the connection at startup at
            // the same moment; the lock makes sure exactly one of them
            // actually connects and the rest reuse that connection, rather
            // than each opening its own.
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
                    // Shown in the management UI's Connections tab, so you
                    // can tell which process a connection belongs to.
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
