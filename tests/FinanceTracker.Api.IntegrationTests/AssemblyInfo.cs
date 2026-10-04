using Xunit;

// The test factories pass the connection string through a process-wide
// environment variable (see CustomWebApplicationFactory). Two factories
// building hosts in parallel could boot against each other's database, so
// test classes in this project run one at a time.
[assembly: CollectionBehavior(DisableTestParallelization = true)]
