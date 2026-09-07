using System.Net;
using FluentAssertions;

namespace FinanceTracker.Api.IntegrationTests.OpenApi
{
    /// <summary>
    /// Just proves the two endpoints Program.cs wires up in Development are
    /// actually reachable -- CustomWebApplicationFactory runs the host in
    /// Development by default (WebApplicationFactory's own default since
    /// .NET 6), so both are expected to respond here the same as they would
    /// on a developer's own machine.
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
