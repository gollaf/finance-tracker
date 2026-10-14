using System.Net;
using FluentAssertions;
using Microsoft.AspNetCore.Http;

namespace FinanceTracker.Api.IntegrationTests.RateLimiting
{
    /// <summary>
    /// The limits live in the host, which every test in this class shares, so
    /// each test uses client addresses of its own. Requests are sent straight
    /// to the test server to set the connection's remote address, which an
    /// HttpClient can't. The probed endpoints answer 401 without a database;
    /// the limiter runs before authorization, so those requests still count.
    /// </summary>
    public sealed class RateLimitingTests : IClassFixture<RateLimitingWebApplicationFactory>
    {
        private const string TrustedProxy = "10.0.0.5";
        private const string AuthEndpoint = "/api/auth/manage/info";
        private static readonly string ApiEndpoint = $"/api/accounts/{Guid.NewGuid()}/balance";

        private readonly RateLimitingWebApplicationFactory _factory;

        public RateLimitingTests(RateLimitingWebApplicationFactory factory)
        {
            _factory = factory;
        }

        private async Task<HttpResponse> GetAsync(string path, string remoteAddress, string? forwardedFor = null)
        {
            var context = await _factory.Server.SendAsync(context =>
            {
                context.Request.Method = HttpMethods.Get;
                context.Request.Path = path;
                context.Connection.RemoteIpAddress = IPAddress.Parse(remoteAddress);

                if (forwardedFor is not null)
                    context.Request.Headers["X-Forwarded-For"] = forwardedFor;
            });

            return context.Response;
        }

        [Fact]
        public async Task AuthEndpoints_RejectAClientOverTheAuthLimit_With429AndRetryAfter()
        {
            for (var i = 0; i < RateLimitingWebApplicationFactory.AuthPermitLimit; i++)
                (await GetAsync(AuthEndpoint, "192.0.2.10")).StatusCode.Should().Be(StatusCodes.Status401Unauthorized);

            var rejected = await GetAsync(AuthEndpoint, "192.0.2.10");

            rejected.StatusCode.Should().Be(StatusCodes.Status429TooManyRequests);
            rejected.Headers.RetryAfter.ToString().Should().NotBeEmpty();
            rejected.ContentType.Should().StartWith("application/problem+json");
        }

        [Fact]
        public async Task OtherEndpoints_RejectAClientOverTheGeneralLimit()
        {
            for (var i = 0; i < RateLimitingWebApplicationFactory.PermitLimit; i++)
                (await GetAsync(ApiEndpoint, "192.0.2.11")).StatusCode.Should().Be(StatusCodes.Status401Unauthorized);

            (await GetAsync(ApiEndpoint, "192.0.2.11")).StatusCode.Should().Be(StatusCodes.Status429TooManyRequests);
        }

        [Fact]
        public async Task HealthEndpoints_AreNeverLimited()
        {
            for (var i = 0; i < RateLimitingWebApplicationFactory.PermitLimit * 3; i++)
                (await GetAsync("/health/live", "192.0.2.12")).StatusCode.Should().Be(StatusCodes.Status200OK);
        }

        [Fact]
        public async Task ClientsBehindTheTrustedProxy_AreLimitedOneByOne()
        {
            for (var i = 0; i < RateLimitingWebApplicationFactory.AuthPermitLimit; i++)
                await GetAsync(AuthEndpoint, TrustedProxy, forwardedFor: "203.0.113.1");

            var sameVisitor = await GetAsync(AuthEndpoint, TrustedProxy, forwardedFor: "203.0.113.1");
            var otherVisitor = await GetAsync(AuthEndpoint, TrustedProxy, forwardedFor: "203.0.113.2");

            sameVisitor.StatusCode.Should().Be(StatusCodes.Status429TooManyRequests);
            otherVisitor.StatusCode.Should().Be(StatusCodes.Status401Unauthorized);
        }

        [Fact]
        public async Task ForwardedFor_FromAnUntrustedAddress_IsIgnored()
        {
            // A visitor connecting directly can't dodge the limit by claiming
            // a new address in every request.
            for (var i = 0; i < RateLimitingWebApplicationFactory.AuthPermitLimit; i++)
                await GetAsync(AuthEndpoint, "192.0.2.13", forwardedFor: $"203.0.113.{10 + i}");

            var response = await GetAsync(AuthEndpoint, "192.0.2.13", forwardedFor: "203.0.113.99");

            response.StatusCode.Should().Be(StatusCodes.Status429TooManyRequests);
        }
    }
}
