namespace FinanceTracker.Worker
{
    /// <summary>
    /// Placeholder background service: logs a line on startup, on a fixed
    /// interval, and on shutdown, so it's visible in `docker compose logs
    /// worker` that the process started, stays alive, and shuts down
    /// cleanly. It does no real work; it exists only until the Worker has
    /// real background services of its own (message consumers, the outbox
    /// relay), at which point it can be deleted.
    /// </summary>
    public sealed class HeartbeatService : BackgroundService
    {
        private static readonly TimeSpan Interval = TimeSpan.FromSeconds(30);

        private readonly ILogger<HeartbeatService> _logger;

        public HeartbeatService(ILogger<HeartbeatService> logger)
        {
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("Worker started; heartbeat every {Interval}.", Interval);

            // PeriodicTimer rather than Task.Delay in a loop: it ticks on a
            // fixed schedule regardless of how long each iteration's work
            // takes, and WaitForNextTickAsync honors the stopping token.
            using var timer = new PeriodicTimer(Interval);

            try
            {
                while (await timer.WaitForNextTickAsync(stoppingToken))
                {
                    _logger.LogInformation("Worker heartbeat.");
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                // Expected: the host is shutting down (Ctrl+C, `docker
                // compose stop`). WaitForNextTickAsync signals that by
                // throwing, which is a normal stop, not an error.
            }

            _logger.LogInformation("Worker stopping.");
        }
    }
}
