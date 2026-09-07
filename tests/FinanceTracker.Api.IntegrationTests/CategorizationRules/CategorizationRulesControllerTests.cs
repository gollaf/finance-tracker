using System.Net;
using System.Net.Http.Json;
using FinanceTracker.Api.CategorizationRules;
using FinanceTracker.Api.Categories;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc;

namespace FinanceTracker.Api.IntegrationTests.CategorizationRules
{
    public sealed class CategorizationRulesControllerTests : IClassFixture<CustomWebApplicationFactory>
    {
        private readonly HttpClient _client;

        public CategorizationRulesControllerTests(CustomWebApplicationFactory factory)
        {
            _client = factory.CreateClient();
        }

        private async Task<Guid> CreatePersistedCategoryAsync()
        {
            var response = await _client.PostAsJsonAsync(
                "/api/categories", new CreateCategoryRequest($"Category-{Guid.NewGuid()}", null));
            var created = await response.Content.ReadFromJsonAsync<CreateCategoryResponse>();
            return created!.Id;
        }

        [Fact]
        public async Task Create_WithValidRequest_Returns201WithId()
        {
            var categoryId = await CreatePersistedCategoryAsync();
            var request = new CreateCategorizationRuleRequest("starbucks", categoryId, Priority: 1);

            var response = await _client.PostAsJsonAsync("/api/categorization-rules", request);

            response.StatusCode.Should().Be(HttpStatusCode.Created);

            var body = await response.Content.ReadFromJsonAsync<CreateCategorizationRuleResponse>();
            body!.Id.Should().NotBeEmpty();
        }

        [Fact]
        public async Task Create_WithEmptyPattern_Returns400ProblemDetails()
        {
            var categoryId = await CreatePersistedCategoryAsync();
            var request = new CreateCategorizationRuleRequest("", categoryId, Priority: 1);

            var response = await _client.PostAsJsonAsync("/api/categorization-rules", request);

            response.StatusCode.Should().Be(HttpStatusCode.BadRequest);

            var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>();
            problem!.Status.Should().Be((int)HttpStatusCode.BadRequest);
        }

        [Fact]
        public async Task Create_WithUnknownCategoryId_Returns404ProblemDetails()
        {
            var request = new CreateCategorizationRuleRequest("uber", Guid.NewGuid(), Priority: 1);

            var response = await _client.PostAsJsonAsync("/api/categorization-rules", request);

            response.StatusCode.Should().Be(HttpStatusCode.NotFound);

            var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>();
            problem!.Status.Should().Be((int)HttpStatusCode.NotFound);
        }
    }
}
