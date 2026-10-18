using Amazon.CDK;

namespace FinanceTracker.Aws
{
    public sealed class AppStackProps : StackProps
    {
        /// <summary>The network and database the application runs against.</summary>
        public required DataStack Data { get; init; }

        /// <summary>The build to run: an image tag CI published, like sha-1234567.</summary>
        public required string ImageTag { get; init; }

        /// <summary>Where alarms are e-mailed; when null, they notify no one.</summary>
        public string? AlarmEmail { get; init; }
    }
}
