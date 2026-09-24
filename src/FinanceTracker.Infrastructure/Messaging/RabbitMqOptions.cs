namespace FinanceTracker.Infrastructure.Messaging
{
    /// <summary>
    /// Bound from the "RabbitMq" configuration section. Host and Port
    /// default to a broker on this machine (docker-compose publishes 5672 to
    /// the host), which is what a `dotnet run` outside Docker needs; inside
    /// docker-compose, RabbitMq__Host overrides Host with the service name.
    /// UserName and Password have no defaults on purpose -- see
    /// AddRabbitMqMessaging, which refuses to start without them rather than
    /// silently falling back to RabbitMQ's localhost-only "guest" user.
    /// </summary>
    public sealed class RabbitMqOptions
    {
        public const string SectionName = "RabbitMq";

        public string Host { get; set; } = "localhost";

        public int Port { get; set; } = 5672;

        public string UserName { get; set; } = string.Empty;

        public string Password { get; set; } = string.Empty;

        public string VirtualHost { get; set; } = "/";
    }
}
