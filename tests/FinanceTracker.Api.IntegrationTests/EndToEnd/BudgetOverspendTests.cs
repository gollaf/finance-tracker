using System.Net;
using System.Net.Http.Json;
using FinanceTracker.Api.Accounts;
using FinanceTracker.Api.Budgets;
using FinanceTracker.Api.Categories;
using FinanceTracker.Api.Transactions;
using FinanceTracker.Domain.Accounts;
using FinanceTracker.Domain.Transactions;
using FluentAssertions;

namespace FinanceTracker.Api.IntegrationTests.EndToEnd
{
    /// <summary>
    /// The second full-stack flow, alongside AccountLifecycleTests: a
    /// Budget's status and an Account's spending summary are computed by two
    /// separate query handlers (GetBudgetStatus, GetSpendingSummary) that
    /// both independently sum the same underlying Transactions. This proves
    /// they agree with each other and with reality end to end, not just that
    /// each one individually returns a plausible-looking number.
    /// </summary>
    public sealed class BudgetOverspendTests : IClassFixture<CustomWebApplicationFactory>
    {
        private readonly HttpClient _client;

        public BudgetOverspendTests(CustomWebApplicationFactory factory)
        {
            _client = factory.CreateClient();
        }

        [Fact]
        public async Task CreateBudget_AddOverLimitExpense_StatusAndSummaryBothShowOverspend()
        {
            // 1. Set up a Category, an Account, and a Budget for the current month.
            var categoryId = await CreatePersistedCategoryAsync();
            var accountId = await CreatePersistedAccountAsync();
            var today = DateOnly.FromDateTime(DateTime.UtcNow);

            var createBudgetResponse = await _client.PostAsJsonAsync(
                "/api/budgets", new CreateBudgetRequest(categoryId, today.Year, today.Month, 200m, "USD"));
            createBudgetResponse.StatusCode.Should().Be(HttpStatusCode.Created);
            var budget = await createBudgetResponse.Content.ReadFromJsonAsync<CreateBudgetResponse>();

            // 2. Nothing's been spent yet -- the budget starts fully unused.
            var initialStatus = await GetBudgetStatusAsync(budget!.Id);
            initialStatus.ActualSpending.Should().Be(0m);
            initialStatus.Remaining.Should().Be(200m);
            initialStatus.IsOverBudget.Should().BeFalse();

            // 3. Add an expense that blows past the limit and assign it to the Category.
            var addTransactionResponse = await _client.PostAsJsonAsync(
                "/api/transactions",
                new AddTransactionRequest(accountId, 250m, TransactionType.Expense, "Overspent on dining", today));
            addTransactionResponse.StatusCode.Should().Be(HttpStatusCode.Created);
            var transaction = await addTransactionResponse.Content.ReadFromJsonAsync<AddTransactionResponse>();

            var categorizeResponse = await _client.PutAsJsonAsync(
                $"/api/transactions/{transaction!.Id}/category", new CategorizeTransactionRequest(categoryId));
            categorizeResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);

            // 4. The Budget's own status now shows the overspend.
            var statusAfterExpense = await GetBudgetStatusAsync(budget.Id);
            statusAfterExpense.ActualSpending.Should().Be(250m);
            statusAfterExpense.Remaining.Should().Be(-50m);
            statusAfterExpense.IsOverBudget.Should().BeTrue();

            // 5. The Account's own spending summary for the month agrees with it.
            var summaryResponse = await _client.GetAsync(
                $"/api/transactions/spending-summary?accountId={accountId}&year={today.Year}&month={today.Month}");
            summaryResponse.StatusCode.Should().Be(HttpStatusCode.OK);
            var summary = await summaryResponse.Content.ReadFromJsonAsync<List<CategorySpendingResponse>>();
            summary.Should().ContainSingle();
            summary![0].CategoryId.Should().Be(categoryId);
            summary[0].Total.Should().Be(250m);
        }

        private async Task<Guid> CreatePersistedCategoryAsync()
        {
            var response = await _client.PostAsJsonAsync(
                "/api/categories", new CreateCategoryRequest($"Category-{Guid.NewGuid()}", null));
            var created = await response.Content.ReadFromJsonAsync<CreateCategoryResponse>();
            return created!.Id;
        }

        private async Task<Guid> CreatePersistedAccountAsync()
        {
            var response = await _client.PostAsJsonAsync(
                "/api/accounts", new CreateAccountRequest("Checking", AccountType.Checking, "USD"));
            var created = await response.Content.ReadFromJsonAsync<CreateAccountResponse>();
            return created!.Id;
        }

        private async Task<BudgetStatusResponse> GetBudgetStatusAsync(Guid budgetId)
        {
            var response = await _client.GetAsync($"/api/budgets/{budgetId}/status");
            response.StatusCode.Should().Be(HttpStatusCode.OK);
            var status = await response.Content.ReadFromJsonAsync<BudgetStatusResponse>();
            return status!;
        }
    }
}
