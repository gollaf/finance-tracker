// The CDK runs in one Node.js process that the jsii runtime starts on first
// use, and that runtime is not safe to call from two threads at once. xUnit
// runs test classes in parallel by default, so two fixtures creating an App
// at the same moment break each other. One at a time, then.
[assembly: CollectionBehavior(DisableTestParallelization = true)]
