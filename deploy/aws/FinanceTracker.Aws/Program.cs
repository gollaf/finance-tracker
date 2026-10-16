using Amazon.CDK;
using FinanceTracker.Aws;

var app = new App();

// Lets the AWS console list the project's resources together.
Tags.Of(app).Add("project", "finance-tracker");

// No account or region (Env): the stacks go to whichever account and region
// the CLI is signed in to, and synthesizing needs no AWS credentials, so the
// tests and CI can do it too.
var data = new DataStack(app, "FinanceTracker-Data", new StackProps
{
    Description = "Finance Tracker: network and PostgreSQL database",
});

// The application stack needs to know which build to run. Without one it is
// left out, so the data stack can still be deployed or compared on its own.
if (app.Node.TryGetContext("imageTag") is string imageTag)
{
    _ = new AppStack(app, "FinanceTracker-App", new AppStackProps
    {
        Data = data,
        ImageTag = imageTag,
        Description = "Finance Tracker: containers on ECS Fargate",
    });
}
else
{
    Console.Error.WriteLine("FinanceTracker-App is left out: add -c imageTag=sha-1234567 to include it.");
}

app.Synth();
