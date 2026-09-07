using Xunit;

// The connection string fix in CustomWebApplicationFactory sets a
// process-wide environment variable (Environment.SetEnvironmentVariable),
// because that's the only override Program.cs reliably sees in time -- see
// the comment on CustomWebApplicationFactory for why. That's fine as long
// as only one factory's host is ever being built at a time: two
// WebApplicationFactory<Program> instances (e.g. once a second
// controller's test class exists) racing to set that same environment
// variable to two different Testcontainers connection strings would let
// one test class's host boot against another class's database. xUnit runs
// different test classes in parallel by default, so this has to be turned
// off explicitly for this project, not left as a latent flaky-test trap
// waiting for the next controller.
[assembly: CollectionBehavior(DisableTestParallelization = true)]
