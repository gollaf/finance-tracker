using System.Net;
using System.Net.Http.Json;
using FinanceTracker.Api.Budgets;
using FinanceTracker.Api.Categories;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc;

namespace FinanceTracker.Api.IntegrationTests.Budgets
{
    /// <summary>
    /// Each test uses its own freshly-created Category (and often its own
    /// Year/Month) so tests sharing this class's database don't collide on
    /// CreateBudget's real category+period uniqueness rule -- same reasoning
    /// as CategoriesControllerTests' Guid-suffixed names.
    /// </summary>
    public sealed class BudgetsControllerTests : IClassFixture<CustomWebApplicationFactory>
    {
        private readonly HttpClient _client;

        public BudgetsControllerTests(CustomWebApplicationFactory factory)
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

        private async Task<Guid> CreatePersistedBudgetAsync(Guid categoryId, int year, int month, decimal limitAmount = 500m)
        {
            var response = await _client.PostAsJsonAsync(
                "/api/budgets", new CreateBudgetRequest(categoryId, year, month, limitAmount, "USD"));
            var created = await response.Content.ReadFromJsonAsync<CreateBudgetResponse>();
            return created!.Id;
        }

        [Fact]
        public async Task Create_WithValidRequest_Returns201WithId()
        {
            var categoryId = await CreatePersistedCategoryAsync();
            var request = new CreateBudgetRequest(categoryId, 2026, 1, 500m, "USD");

            var response = await _client.PostAsJsonAsync("/api/budgets", request);

            response.StatusCode.Should().Be(HttpStatusCode.Created);

            var body = await response.Content.ReadFromJsonAsync<CreateBudgetResponse>();
            body!.Id.Should().NotBeEmpty();
        }

        [Fact]
        public async Task Create_WithUnknownCategoryId_Returns404ProblemDetails()
        {
            var request = new CreateBudgetRequest(Guid.NewGuid(), 2026, 2, 500m, "USD");

            var response = await _client.PostAsJsonAsync("/api/budgets", request);

            response.StatusCode.Should().Be(HttpStatusCode.NotFound);

            var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>();
            problem!.Status.Should().Be((int)HttpStatusCode.NotFound);
        }

        [Fact]
        public async Task Create_WithDuplicateCategoryAndPeriod_Returns409ProblemDetails()
        {
            var categoryId = await CreatePersistedCategoryAsync();
            await CreatePersistedBudgetAsync(categoryId, 2026, 3);

            var response = await _client.PostAsJsonAsync(
                "/api/budgets", new CreateBudgetRequest(categoryId, 2026, 3, 750m, "USD"));

            response.StatusCode.Should().Be(HttpStatusCode.Conflict);

            var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>();
            problem!.Status.Should().Be((int)HttpStatusCode.Conflict);
        }

        [Fact]
        public async Task Create_WithZeroLimitAmount_Returns400ProblemDetails()
        {
            var categoryId = await CreatePersistedCategoryAsync();
            var request = new CreateBudgetRequest(categoryId, 2026, 4, 0m, "USD");

            var response = await _client.PostAsJsonAsync("/api/budgets", request);

            response.StatusCode.Should().Be(HttpStatusCode.BadRequest);

            var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>();
            problem!.Status.Should().Be((int)HttpStatusCode.BadRequest);
        }

        [Fact]
        public async Task Update_WithValidRequest_Returns204AndChangesStatus()
        {
            var categoryId = await CreatePersistedCategoryAsync();
            var budgetId = await CreatePersistedBudgetAsync(categoryId, 2026, 5, limitAmount: 500m);

            var response = await _client.PutAsJsonAsync(
                $"/api/budgets/{budgetId}", new UpdateBudgetRequest(900m));

            response.StatusCode.Should().Be(HttpStatusCode.NoContent);

            var statusResponse = await _client.GetAsync($"/api/budgets/{budgetId}/status");
            var status = await statusResponse.Content.ReadFromJsonAsync<BudgetStatusResponse>();
            status!.LimitAmount.Should().Be(900m);
        }

        [Fact]
        public async Task Update_WithUnknownBudgetId_Returns404ProblemDetails()
        {
            var response = await _client.PutAsJsonAsync(
                $"/api/budgets/{Guid.NewGuid()}", new UpdateBudgetRequest(900m));

            response.StatusCode.Should().Be(HttpStatusCode.NotFound);

            var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>();
            problem!.Status.Should().Be((int)HttpStatusCode.NotFound);
        }

        [Fact]
        public async Task Update_WithZeroLimitAmount_Returns400ProblemDetails()
        {
            var categoryId = await CreatePersistedCategoryAsync();
            var budgetId = await CreatePersistedBudgetAsync(categoryId, 2026, 6);

            var response = await _client.PutAsJsonAsync(
                $"/api/budgets/{budgetId}", new UpdateBudgetRequest(0m));

            response.StatusCode.Should().Be(HttpStatusCode.BadRequest);

            var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>();
            problem!.Status.Should().Be((int)HttpStatusCode.BadRequest);
        }

        [Fact]
        public async Task GetStatus_ForBudgetWithNoTransactions_ReturnsFullyRemaining()
        {
            var categoryId = await CreatePersistedCategoryAsync();
            var budgetId = await CreatePersistedBudgetAsync(categoryId, 2026, 7, limitAmount: 300m);

            var response = await _client.GetAsync($"/api/budgets/{budgetId}/status");

            response.StatusCode.Should().Be(HttpStatusCode.OK);

            var status = await response.Content.ReadFromJsonAsync<BudgetStatusResponse>();
            status!.LimitAmount.Should().Be(300m);
            status.ActualSpending.Should().Be(0m);
            status.Remaining.Should().Be(300m);
            status.IsOverBudget.Should().BeFalse();
        }

        [Fact]
        public async Task GetStatus_WithUnknownBudgetId_Returns404ProblemDetails()
        {
            var response = await _client.GetAsync($"/api/budgets/{Guid.NewGuid()}/status");

            response.StatusCode.Should().Be(HttpStatusCode.NotFound);

            var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>();
            problem!.Status.Should().Be((int)HttpStatusCode.NotFound);
        }
    }
}
