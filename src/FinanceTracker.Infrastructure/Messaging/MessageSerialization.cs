using System.Text.Json;

namespace FinanceTracker.Infrastructure.Messaging
{
    /// <summary>
    /// The one JSON configuration used on both sides of the broker. A
    /// publisher and consumer disagreeing on, say, property-name casing
    /// wouldn't fail loudly -- every property would just deserialize as its
    /// default value -- so both sides go through this single definition.
    /// </summary>
    public static class MessageSerialization
    {
        /// <summary>
        /// JsonSerializerDefaults.Web: camelCase property names and
        /// case-insensitive reading, the same conventions the Api already
        /// uses for HTTP bodies.
        /// </summary>
        public static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web);

        public static string Serialize<TMessage>(TMessage message) =>
            JsonSerializer.Serialize(message, Options);
    }
}
