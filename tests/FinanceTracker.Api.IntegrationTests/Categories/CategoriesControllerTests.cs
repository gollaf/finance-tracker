using System.Net;
using System.Net.Http.Json;
using FinanceTracker.Api.Categories;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc;

namespace FinanceTracker.Api.IntegrationTests.Categories
{
    /// <summary>
    /// Names are suffixed with a fresh Guid in every test. The class-shared
    /// fixture means every test in this class hits the same database (see
    /// CustomWebApplicationFactory), and CreateCategory enforces real
    /// name uniqueness -- two tests both trying to create plain "Groceries"
    /// would collide and turn an intended 201 into an unintended 409.
    /// </summary>
    public sealed class CategoriesControllerTests : IClassFixture<CustomWebApplicationFactory>
    {
        private readonly HttpClient _client;

        public CategoriesControllerTests(CustomWebApplicationFactory factory)
        {
            _client = factory.CreateClient();
        }

        [Fact]
        public async Task Create_WithValidRequest_Returns201WithId()
        {
            var request = new CreateCategoryRequest($"Groceries-{Guid.NewGuid()}", null);

            var response = await _client.PostAsJsonAsync("/api/categories", request);

            response.StatusCode.Should().Be(HttpStatusCode.Created);

            var body = await response.Content.ReadFromJsonAsync<CreateCategoryResponse>();
            body!.Id.Should().NotBeEmpty();
        }

        [Fact]
        public async Task Create_WithValidParentCategoryId_Returns201WithId()
        {
            var parentResponse = await _client.PostAsJsonAsync(
                "/api/categories", new CreateCategoryRequest($"Food-{Guid.NewGuid()}", null));
            var parent = await parentResponse.Content.ReadFromJsonAsync<CreateCategoryResponse>();

            var response = await _client.PostAsJsonAsync(
                "/api/categories", new CreateCategoryRequest($"Dining Out-{Guid.NewGuid()}", parent!.Id));

            response.StatusCode.Should().Be(HttpStatusCode.Created);
        }

        [Fact]
        public async Task Create_WithEmptyName_Returns400ProblemDetails()
        {
            var response = await _client.PostAsJsonAsync("/api/categories", new CreateCategoryRequest("", null));

            response.StatusCode.Should().Be(HttpStatusCode.BadRequest);

            var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>();
            problem!.Status.Should().Be((int)HttpStatusCode.BadRequest);
        }

        [Fact]
        public async Task Create_WithDuplicateName_Returns409ProblemDetails()
        {
            var name = $"Utilities-{Guid.NewGuid()}";
            await _client.PostAsJsonAsync("/api/categories", new CreateCategoryRequest(name, null));

            var response = await _client.PostAsJsonAsync("/api/categories", new CreateCategoryRequest(name, null));

            response.StatusCode.Should().Be(HttpStatusCode.Conflict);

            var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>();
            problem!.Status.Should().Be((int)HttpStatusCode.Conflict);
        }

        [Fact]
        public async Task Create_WithUnknownParentCategoryId_Returns404ProblemDetails()
        {
            var request = new CreateCategoryRequest($"Orphan-{Guid.NewGuid()}", Guid.NewGuid());

            var response = await _client.PostAsJsonAsync("/api/categories", request);

            response.StatusCode.Should().Be(HttpStatusCode.NotFound);

            var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>();
            problem!.Status.Should().Be((int)HttpStatusCode.NotFound);
        }
    }
}
