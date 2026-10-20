using System.Collections;
using System.Diagnostics;
using System.Net.Http.Json;
using FinanceTracker.Api.Accounts;
using FinanceTracker.Domain.Accounts;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using OpenTelemetry.Trace;

namespace FinanceTracker.Api.IntegrationTests.Telemetry
{
    /// <summary>
    /// Each request carries a W3C traceparent header with a trace ID the test
    /// picks, the way a caller that is itself traced would send one. The Api
    /// continues that trace, so the spans of one request can be told apart
    /// from everything else the host records.
    /// </summary>
    public sealed class TracingTests : IClassFixture<CustomWebApplicationFactory>
    {
        private readonly CustomWebApplicationFactory _factory;

        public TracingTests(CustomWebApplicationFactory factory)
        {
            _factory = factory;
        }

        [Fact]
        public async Task Request_IsTracedUnderTheCallersTraceId_WithItsDatabaseCalls()
        {
            var exported = new ConcurrentActivityCollection();
            await using var tracedFactory = WithInMemoryExporter(exported);
            using var client = tracedFactory.CreateClient();
            var traceId = ActivityTraceId.CreateRandom();

            await SendCreateAccountAsync(client, traceId);

            var serverSpan = await WaitForAsync(exported, span => span.TraceId == traceId && span.Kind == ActivityKind.Server);
            serverSpan.GetTagItem("http.request.method").Should().Be("POST");
            serverSpan.GetTagItem("http.route").Should().Be("api/accounts");

            // Database spans end before the request's span does, so they are
            // already exported once it is.
            exported.Snapshot().Should().Contain(span => span.TraceId == traceId && span.Source.Name == "Npgsql");
        }

        [Fact]
        public async Task ReadinessProbe_IsNotTraced_NorAreItsDatabaseCalls()
        {
            var exported = new ConcurrentActivityCollection();
            await using var tracedFactory = WithInMemoryExporter(exported);
            using var client = tracedFactory.CreateClient();
            var probeTraceId = ActivityTraceId.CreateRandom();
            var laterTraceId = ActivityTraceId.CreateRandom();

            using var probe = new HttpRequestMessage(HttpMethod.Get, "/health/ready");
            probe.Headers.Add("traceparent", Traceparent(probeTraceId));
            (await client.SendAsync(probe)).EnsureSuccessStatusCode();

            // A traced request after the probe: once its span is exported,
            // anything the probe produced would have been exported too.
            await SendCreateAccountAsync(client, laterTraceId);
            await WaitForAsync(exported, span => span.TraceId == laterTraceId && span.Kind == ActivityKind.Server);

            exported.Snapshot().Should().NotContain(span => span.TraceId == probeTraceId);
        }

        private WebApplicationFactory<Program> WithInMemoryExporter(ICollection<Activity> exported) =>
            _factory.WithWebHostBuilder(builder => builder.ConfigureTestServices(services =>
                services.ConfigureOpenTelemetryTracerProvider(tracing => tracing.AddInMemoryExporter(exported))));

        private static async Task SendCreateAccountAsync(HttpClient client, ActivityTraceId traceId)
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, "/api/accounts")
            {
                Content = JsonContent.Create(new CreateAccountRequest("Checking", AccountType.Checking, "USD")),
            };
            request.Headers.Add("traceparent", Traceparent(traceId));

            (await client.SendAsync(request)).EnsureSuccessStatusCode();
        }

        // Version 00, the trace, a made-up parent span, and flags 01: sampled.
        private static string Traceparent(ActivityTraceId traceId) =>
            $"00-{traceId}-{ActivitySpanId.CreateRandom()}-01";

        // The response can reach the test a moment before the server's span
        // ends and is exported.
        private static async Task<Activity> WaitForAsync(ConcurrentActivityCollection exported, Func<Activity, bool> match)
        {
            var deadline = DateTime.UtcNow.AddSeconds(5);
            while (true)
            {
                var found = exported.Snapshot().FirstOrDefault(match);
                if (found is not null)
                    return found;

                if (DateTime.UtcNow > deadline)
                    throw new TimeoutException("No exported span matched within 5 seconds.");

                await Task.Delay(50);
            }
        }

        /// <summary>
        /// The exporter adds spans on the server's threads while the test
        /// reads them; a plain List would be read mid-write.
        /// </summary>
        private sealed class ConcurrentActivityCollection : ICollection<Activity>
        {
            private readonly List<Activity> _items = [];
            private readonly Lock _lock = new();

            public int Count { get { lock (_lock) return _items.Count; } }

            public bool IsReadOnly => false;

            public Activity[] Snapshot() { lock (_lock) return [.. _items]; }

            public void Add(Activity item) { lock (_lock) _items.Add(item); }

            public void Clear() { lock (_lock) _items.Clear(); }

            public bool Contains(Activity item) { lock (_lock) return _items.Contains(item); }

            public void CopyTo(Activity[] array, int arrayIndex) { lock (_lock) _items.CopyTo(array, arrayIndex); }

            public bool Remove(Activity item) { lock (_lock) return _items.Remove(item); }

            public IEnumerator<Activity> GetEnumerator() => ((IEnumerable<Activity>)Snapshot()).GetEnumerator();

            IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
        }
    }
}
