using Amazon.CDK;
using FinanceTracker.Aws;

var app = new App();

// Lets the AWS console list the project's resources together.
Tags.Of(app).Add("project", "finance-tracker");

// No account or region (Env): the stacks go to whichever account and region
// the CLI is signed in to, and synthesizing needs no AWS credentials, so the
// tests and CI can do it too.
_ = new DataStack(app, "FinanceTracker-Data", new StackProps
{
    Description = "Finance Tracker: network and PostgreSQL database",
});

app.Synth();
