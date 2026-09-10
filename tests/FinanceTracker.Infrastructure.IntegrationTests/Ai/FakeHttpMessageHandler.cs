namespace FinanceTracker.Infrastructure.IntegrationTests.Ai
{
    /// <summary>
    /// Stands in for HttpClient's real transport in GroqInsightsGeneratorTests
    /// -- lets each test decide exactly what Groq "responds" with (or how
    /// long it takes to respond) without a real network call.
    /// </summary>
    internal sealed class FakeHttpMessageHandler : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> _responder;

        public FakeHttpMessageHandler(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> responder)
        {
            _responder = responder;
        }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken) =>
            _responder(request, cancellationToken);
    }
}
