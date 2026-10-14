using System.Net;
using System.Net.Http.Json;
using FinanceTracker.Api.Accounts;
using FinanceTracker.Api.Budgets;
using FinanceTracker.Api.Categories;
using FinanceTracker.Api.CategorizationRules;
using FinanceTracker.Api.Transactions;
using FinanceTracker.Domain.Accounts;
using FinanceTracker.Domain.Transactions;
using FluentAssertions;

namespace FinanceTracker.Api.IntegrationTests.Ownership
{
    /// <summary>
    /// Categories, categorization rules and budgets belong to one user. A
    /// second user can't see them, build on them or change them: every such
    /// attempt answers 404, exactly as if they didn't exist.
    /// </summary>
    public sealed class CategoryOwnershipTests : IClassFixture<CustomWebApplicationFactory>, IAsyncLifetime
    {
        private static readonly DateOnly Today = DateOnly.FromDateTime(DateTime.UtcNow);

        private readonly CustomWebApplicationFactory _factory;
        private readonly HttpClient _owner;
        private HttpClient _stranger = null!;

        public CategoryOwnershipTests(CustomWebApplicationFactory factory)
        {
            _factory = factory;
            _owner = factory.CreateClient();
        }

        public async Task InitializeAsync() => _stranger = await _factory.CreateClientForNewUserAsync();

        public Task DisposeAsync() => Task.CompletedTask;

        private static async Task<Guid> CreateCategoryAsync(HttpClient client, string name)
        {
            var response = await client.PostAsJsonAsync("/api/categories", new CreateCategoryRequest(name, null));
            response.EnsureSuccessStatusCode();
            return (await response.Content.ReadFromJsonAsync<CreateCategoryResponse>())!.Id;
        }

        private static async Task<Guid> AddTransactionAsync(HttpClient client)
        {
            var account = await client.PostAsJsonAsync(
                "/api/accounts", new CreateAccountRequest("Checking", AccountType.Checking, "USD"));
            account.EnsureSuccessStatusCode();
            var accountId = (await account.Content.ReadFromJsonAsync<CreateAccountResponse>())!.Id;

            var transaction = await client.PostAsJsonAsync(
                "/api/transactions", new AddTransactionRequest(accountId, 10m, TransactionType.Expense, "Coffee", Today));
            transaction.EnsureSuccessStatusCode();
            return (await transaction.Content.ReadFromJsonAsync<AddTransactionResponse>())!.Id;
        }

        [Fact]
        public async Task CategoryNames_AreUniquePerUser()
        {
            var name = $"Groceries-{Guid.NewGuid()}";
            await CreateCategoryAsync(_owner, name);

            var strangersOwn = await _stranger.PostAsJsonAsync("/api/categories", new CreateCategoryRequest(name, null));
            var ownersSecond = await _owner.PostAsJsonAsync("/api/categories", new CreateCategoryRequest(name, null));

            strangersOwn.StatusCode.Should().Be(HttpStatusCode.Created);
            ownersSecond.StatusCode.Should().Be(HttpStatusCode.Conflict);
        }

        [Fact]
        public async Task ASubcategoryOfAnotherUsersCategory_Returns404()
        {
            var parentId = await CreateCategoryAsync(_owner, $"Food-{Guid.NewGuid()}");

            var response = await _stranger.PostAsJsonAsync(
                "/api/categories", new CreateCategoryRequest($"Dining-{Guid.NewGuid()}", parentId));

            response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        }

        [Fact]
        public async Task CategorizingIntoAnotherUsersCategory_Returns404()
        {
            var categoryId = await CreateCategoryAsync(_owner, $"Dining-{Guid.NewGuid()}");
            var strangersTransactionId = await AddTransactionAsync(_stranger);

            var response = await _stranger.PutAsJsonAsync(
                $"/api/transactions/{strangersTransactionId}/category", new CategorizeTransactionRequest(categoryId));

            response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        }

        [Fact]
        public async Task ARuleForAnotherUsersCategory_Returns404()
        {
            var categoryId = await CreateCategoryAsync(_owner, $"Coffee-{Guid.NewGuid()}");

            var response = await _stranger.PostAsJsonAsync(
                "/api/categorization-rules", new CreateCategorizationRuleRequest("starbucks", categoryId, 1));

            response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        }

        [Fact]
        public async Task ABudgetForAnotherUsersCategory_Returns404()
        {
            var categoryId = await CreateCategoryAsync(_owner, $"Travel-{Guid.NewGuid()}");

            var response = await _stranger.PostAsJsonAsync(
                "/api/budgets", new CreateBudgetRequest(categoryId, 2026, 3, 500m, "USD"));

            response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        }

        [Fact]
        public async Task AnotherUsersBudget_CannotBeReadOrChanged()
        {
            var categoryId = await CreateCategoryAsync(_owner, $"Rent-{Guid.NewGuid()}");
            var created = await _owner.PostAsJsonAsync(
                "/api/budgets", new CreateBudgetRequest(categoryId, 2026, 3, 500m, "USD"));
            created.EnsureSuccessStatusCode();
            var budgetId = (await created.Content.ReadFromJsonAsync<CreateBudgetResponse>())!.Id;

            var status = await _stranger.GetAsync($"/api/budgets/{budgetId}/status");
            var update = await _stranger.PutAsJsonAsync($"/api/budgets/{budgetId}", new UpdateBudgetRequest(1m));

            status.StatusCode.Should().Be(HttpStatusCode.NotFound);
            update.StatusCode.Should().Be(HttpStatusCode.NotFound);

            var ownersView = await _owner.GetFromJsonAsync<BudgetStatusResponse>($"/api/budgets/{budgetId}/status");
            ownersView!.LimitAmount.Should().Be(500m);
        }
    }
}
