using System.Net;
using FluentAssertions;

namespace FinanceTracker.Api.IntegrationTests.OpenApi
{
    /// <summary>
    /// The Development-only OpenAPI and Scalar endpoints are reachable
    /// (WebApplicationFactory runs the host in Development).
    /// </summary>
    public sealed class OpenApiTests : IClassFixture<CustomWebApplicationFactory>
    {
        private readonly HttpClient _client;

        public OpenApiTests(CustomWebApplicationFactory factory)
        {
            _client = factory.CreateClient();
        }

        [Fact]
        public async Task OpenApiDocument_Returns200WithJsonContent()
        {
            var response = await _client.GetAsync("/openapi/v1.json");

            response.StatusCode.Should().Be(HttpStatusCode.OK);
            response.Content.Headers.ContentType?.MediaType.Should().Be("application/json");
        }

        [Fact]
        public async Task ScalarUi_Returns200WithHtmlContent()
        {
            var response = await _client.GetAsync("/scalar/v1");

            response.StatusCode.Should().Be(HttpStatusCode.OK);
            response.Content.Headers.ContentType?.MediaType.Should().Be("text/html");
        }
    }
}
