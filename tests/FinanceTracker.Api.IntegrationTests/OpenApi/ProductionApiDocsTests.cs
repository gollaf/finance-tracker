using System.Net;
using FluentAssertions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;

namespace FinanceTracker.Api.IntegrationTests.OpenApi
{
    /// <summary>
    /// Outside Development the API docs are off unless ApiDocs:Enabled turns
    /// them on.
    /// </summary>
    public sealed class ProductionApiDocsTests : IClassFixture<ProductionWebApplicationFactory>
    {
        private readonly ProductionWebApplicationFactory _factory;

        public ProductionApiDocsTests(ProductionWebApplicationFactory factory)
        {
            _factory = factory;
        }

        [Theory]
        [InlineData("/openapi/v1.json")]
        [InlineData("/scalar/v1")]
        public async Task ApiDocs_InProductionByDefault_Return404(string path)
        {
            var client = _factory.CreateClient();

            var response = await client.GetAsync(path);

            response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        }

        [Theory]
        [InlineData("/openapi/v1.json")]
        [InlineData("/scalar/v1")]
        public async Task ApiDocs_InProductionWhenEnabled_Return200(string path)
        {
            var client = _factory
                .WithWebHostBuilder(builder => builder.ConfigureAppConfiguration((_, configuration) =>
                {
                    configuration.AddInMemoryCollection(new Dictionary<string, string?>
                    {
                        ["ApiDocs:Enabled"] = "true"
                    });
                }))
                .CreateClient();

            var response = await client.GetAsync(path);

            response.StatusCode.Should().Be(HttpStatusCode.OK);
        }
    }
}
