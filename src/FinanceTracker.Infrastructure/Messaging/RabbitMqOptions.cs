namespace FinanceTracker.Infrastructure.Messaging
{
    /// <summary>
    /// Bound from the "RabbitMq" configuration section. Host and Port default
    /// to a broker on this machine. UserName and Password have no defaults on
    /// purpose: AddRabbitMqMessaging refuses to start without them rather
    /// than fall back to RabbitMQ's "guest" user.
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
