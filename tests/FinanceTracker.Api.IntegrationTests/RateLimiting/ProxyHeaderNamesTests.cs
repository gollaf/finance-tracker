using System.Net;
using FluentAssertions;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace FinanceTracker.Api.IntegrationTests.RateLimiting
{
    /// <summary>
    /// The visitor's address read from a header named in configuration, as
    /// behind CloudFront. Each test uses addresses of its own, since the
    /// limits live in the shared host.
    /// </summary>
    public sealed class ProxyHeaderNamesTests : IClassFixture<ProxyHeaderNamesWebApplicationFactory>
    {
        private const string TrustedProxy = "10.0.0.5";
        private const string AuthEndpoint = "/api/auth/manage/info";

        private readonly ProxyHeaderNamesWebApplicationFactory _factory;

        public ProxyHeaderNamesTests(ProxyHeaderNamesWebApplicationFactory factory)
        {
            _factory = factory;
        }

        private async Task<HttpResponse> GetAsync(string remoteAddress, IDictionary<string, string> headers)
        {
            var context = await _factory.Server.SendAsync(context =>
            {
                context.Request.Method = HttpMethods.Get;
                context.Request.Path = AuthEndpoint;
                context.Connection.RemoteIpAddress = IPAddress.Parse(remoteAddress);

                foreach (var (name, value) in headers)
                    context.Request.Headers[name] = value;
            });

            return context.Response;
        }

        private Task<HttpResponse> FromViewerAsync(string viewerAddress) =>
            GetAsync(TrustedProxy, new Dictionary<string, string>
            {
                [ProxyHeaderNamesWebApplicationFactory.ForwardedForHeader] = viewerAddress,
            });

        [Fact]
        public async Task VisitorsInTheConfiguredHeader_AreLimitedByAddress_WhateverTheirPort()
        {
            // CloudFront sends "address:port", and the port changes with
            // every connection; the limit must follow the address alone.
            for (var i = 0; i < ProxyHeaderNamesWebApplicationFactory.AuthPermitLimit; i++)
                await FromViewerAsync($"203.0.113.1:{40000 + i}");

            var sameVisitor = await FromViewerAsync("203.0.113.1:40999");
            var otherVisitor = await FromViewerAsync("203.0.113.2:40000");

            sameVisitor.StatusCode.Should().Be(StatusCodes.Status429TooManyRequests);
            otherVisitor.StatusCode.Should().Be(StatusCodes.Status401Unauthorized);
        }

        [Fact]
        public async Task XForwardedFor_IsIgnored_WhenAnotherHeaderIsConfigured()
        {
            // Without the configured header, every request counts against
            // the proxy itself, whatever X-Forwarded-For claims.
            const string proxy = "10.0.0.6";
            for (var i = 0; i < ProxyHeaderNamesWebApplicationFactory.AuthPermitLimit; i++)
                await GetAsync(proxy, new Dictionary<string, string> { ["X-Forwarded-For"] = $"203.0.113.{20 + i}" });

            var response = await GetAsync(proxy, new Dictionary<string, string> { ["X-Forwarded-For"] = "203.0.113.99" });

            response.StatusCode.Should().Be(StatusCodes.Status429TooManyRequests);
        }

        [Fact]
        public void ForwardedHeaderNames_ComeFromConfiguration()
        {
            var options = _factory.Services.GetRequiredService<IOptions<ForwardedHeadersOptions>>().Value;

            options.ForwardedForHeaderName.Should().Be(ProxyHeaderNamesWebApplicationFactory.ForwardedForHeader);
            options.ForwardedProtoHeaderName.Should().Be(ProxyHeaderNamesWebApplicationFactory.ForwardedProtoHeader);
        }
    }
}
