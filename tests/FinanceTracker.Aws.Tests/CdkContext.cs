using System.Text.Json;

namespace FinanceTracker.Aws.Tests
{
    /// <summary>
    /// The "context" section of deploy/aws/cdk.json. The CDK CLI passes it to
    /// the app; its feature flags change what gets synthesized, so the tests
    /// must pass it too.
    /// </summary>
    internal static class CdkContext
    {
        public static IDictionary<string, object> Load()
        {
            var path = Path.Combine(AppContext.BaseDirectory, "cdk.json");
            using var document = JsonDocument.Parse(File.ReadAllText(path));

            return document.RootElement.GetProperty("context")
                .EnumerateObject()
                .ToDictionary(property => property.Name, property => ToValue(property.Value));
        }

        // The CDK runs in Node.js and receives values as JSON; a JsonElement
        // can't be sent there, plain .NET values can.
        private static object ToValue(JsonElement element) => element.ValueKind switch
        {
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            JsonValueKind.String => element.GetString()!,
            JsonValueKind.Number => element.GetDouble(),
            JsonValueKind.Array => element.EnumerateArray().Select(ToValue).ToArray(),
            JsonValueKind.Object => element.EnumerateObject()
                .ToDictionary(property => property.Name, property => ToValue(property.Value)),
            _ => throw new NotSupportedException($"Unexpected {element.ValueKind} value in cdk.json."),
        };
    }
}
