using Amazon.CDK;
using Amazon.CDK.Assertions;

namespace FinanceTracker.Aws.Tests
{
    /// <summary>Synthesizes the application stack once for all its tests.</summary>
    public sealed class AppStackFixture
    {
        public const string ImageTag = "sha-1234567";

        public AppStackFixture()
        {
            var app = new App(new AppProps { Context = CdkContext.Load() });
            var data = new DataStack(app, "FinanceTracker-Data");
            Template = Template.FromStack(new AppStack(app, "FinanceTracker-App", new AppStackProps
            {
                Data = data,
                ImageTag = ImageTag,
            }));
        }

        public Template Template { get; }
    }
}
