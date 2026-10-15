using Amazon.CDK;
using Amazon.CDK.Assertions;

namespace FinanceTracker.Aws.Tests
{
    /// <summary>
    /// Synthesizes the data stack once for all its tests: each synthesis
    /// starts the CDK's Node.js process and takes seconds.
    /// </summary>
    public sealed class DataStackFixture
    {
        public DataStackFixture()
        {
            var app = new App(new AppProps { Context = CdkContext.Load() });
            Template = Template.FromStack(new DataStack(app, "FinanceTracker-Data"));
        }

        public Template Template { get; }
    }
}
