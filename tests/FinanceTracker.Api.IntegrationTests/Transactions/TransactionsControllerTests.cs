using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using FinanceTracker.Api.Accounts;
using FinanceTracker.Api.Categories;
using FinanceTracker.Api.Transactions;
using FinanceTracker.Domain.Accounts;
using FinanceTracker.Domain.Transactions;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc;

namespace FinanceTracker.Api.IntegrationTests.Transactions
{
    // There's no test here for AddTransaction against a closed Account
    // (the 409 "Account.Closed" path in AddTransactionCommandHandler).
    // AccountsController has no way to close an account yet -- Account.Close()
    // exists in the domain, but no CloseAccount command/endpoint has been
    // built on top of it, so that path isn't reachable through the API at
    // all right now. Worth a small follow-up piece on AccountsController.
    public sealed class TransactionsControllerTests : IClassFixture<CustomWebApplicationFactory>
    {
        private readonly HttpClient _client;

        public TransactionsControllerTests(CustomWebApplicationFactory factory)
        {
            _client = factory.CreateClient();
        }

        // ReadFromJsonAsync uses its own default JsonSerializerOptions, separate from
        // the server's -- it has no idea Program.cs registered JsonStringEnumConverter
        // there, so without this it fails to parse an enum the server sent back as a
        // string (for example "Expense") because its default converter only accepts
        // the underlying numeric value.
        private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
        {
            Converters = { new JsonStringEnumConverter() },
        };

        private async Task<Guid> CreatePersistedAccountAsync()
        {
            var response = await _client.PostAsJsonAsync(
                "/api/accounts", new CreateAccountRequest("Checking", AccountType.Checking, "USD"));
            var created = await response.Content.ReadFromJsonAsync<CreateAccountResponse>();
            return created!.Id;
        }

        private async Task<Guid> CreatePersistedCategoryAsync()
        {
            var response = await _client.PostAsJsonAsync(
                "/api/categories", new CreateCategoryRequest($"Category-{Guid.NewGuid()}", null));
            var created = await response.Content.ReadFromJsonAsync<CreateCategoryResponse>();
            return created!.Id;
        }

        private async Task<Guid> CreatePersistedTransactionAsync(Guid accountId, DateOnly? occurredOn = null)
        {
            var response = await _client.PostAsJsonAsync(
                "/api/transactions",
                new AddTransactionRequest(
                    accountId, 25.00m, TransactionType.Expense, "Coffee", occurredOn ?? DateOnly.FromDateTime(DateTime.UtcNow)));
            var created = await response.Content.ReadFromJsonAsync<AddTransactionResponse>();
            return created!.Id;
        }

        [Fact]
        public async Task Add_WithValidRequest_Returns201WithId()
        {
            var accountId = await CreatePersistedAccountAsync();
            var request = new AddTransactionRequest(
                accountId, 42.50m, TransactionType.Expense, "Groceries", DateOnly.FromDateTime(DateTime.UtcNow));

            var response = await _client.PostAsJsonAsync("/api/transactions", request);

            response.StatusCode.Should().Be(HttpStatusCode.Created);

            var body = await response.Content.ReadFromJsonAsync<AddTransactionResponse>();
            body!.Id.Should().NotBeEmpty();
        }

        [Fact]
        public async Task Add_WithUnknownAccountId_Returns404ProblemDetails()
        {
            var request = new AddTransactionRequest(
                Guid.NewGuid(), 10m, TransactionType.Expense, "Snack", DateOnly.FromDateTime(DateTime.UtcNow));

            var response = await _client.PostAsJsonAsync("/api/transactions", request);

            response.StatusCode.Should().Be(HttpStatusCode.NotFound);

            var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>();
            problem!.Status.Should().Be((int)HttpStatusCode.NotFound);
        }

        [Fact]
        public async Task Add_WithZeroAmount_Returns400ProblemDetails()
        {
            var accountId = await CreatePersistedAccountAsync();
            var request = new AddTransactionRequest(
                accountId, 0m, TransactionType.Expense, "Nothing", DateOnly.FromDateTime(DateTime.UtcNow));

            var response = await _client.PostAsJsonAsync("/api/transactions", request);

            response.StatusCode.Should().Be(HttpStatusCode.BadRequest);

            var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>();
            problem!.Status.Should().Be((int)HttpStatusCode.BadRequest);
        }

        [Fact]
        public async Task Add_WithFutureOccurredOn_Returns400ProblemDetails()
        {
            var accountId = await CreatePersistedAccountAsync();
            var request = new AddTransactionRequest(
                accountId, 10m, TransactionType.Expense, "Time travel", DateOnly.FromDateTime(DateTime.UtcNow.AddDays(1)));

            var response = await _client.PostAsJsonAsync("/api/transactions", request);

            response.StatusCode.Should().Be(HttpStatusCode.BadRequest);

            var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>();
            problem!.Status.Should().Be((int)HttpStatusCode.BadRequest);
        }

        [Fact]
        public async Task Update_WithValidRequest_Returns204()
        {
            var accountId = await CreatePersistedAccountAsync();
            var transactionId = await CreatePersistedTransactionAsync(accountId);

            var request = new UpdateTransactionRequest(99.99m, "Updated description", DateOnly.FromDateTime(DateTime.UtcNow));
            var response = await _client.PutAsJsonAsync($"/api/transactions/{transactionId}", request);

            response.StatusCode.Should().Be(HttpStatusCode.NoContent);
        }

        [Fact]
        public async Task Update_WithUnknownTransactionId_Returns404ProblemDetails()
        {
            var request = new UpdateTransactionRequest(50m, "Doesn't exist", DateOnly.FromDateTime(DateTime.UtcNow));

            var response = await _client.PutAsJsonAsync($"/api/transactions/{Guid.NewGuid()}", request);

            response.StatusCode.Should().Be(HttpStatusCode.NotFound);

            var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>();
            problem!.Status.Should().Be((int)HttpStatusCode.NotFound);
        }

        [Fact]
        public async Task Update_WithZeroAmount_Returns400ProblemDetails()
        {
            var accountId = await CreatePersistedAccountAsync();
            var transactionId = await CreatePersistedTransactionAsync(accountId);

            var request = new UpdateTransactionRequest(0m, "Bad amount", DateOnly.FromDateTime(DateTime.UtcNow));
            var response = await _client.PutAsJsonAsync($"/api/transactions/{transactionId}", request);

            response.StatusCode.Should().Be(HttpStatusCode.BadRequest);

            var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>();
            problem!.Status.Should().Be((int)HttpStatusCode.BadRequest);
        }

        [Fact]
        public async Task Delete_ExistingTransaction_Returns204()
        {
            var accountId = await CreatePersistedAccountAsync();
            var transactionId = await CreatePersistedTransactionAsync(accountId);

            var response = await _client.DeleteAsync($"/api/transactions/{transactionId}");

            response.StatusCode.Should().Be(HttpStatusCode.NoContent);
        }

        [Fact]
        public async Task Delete_WithUnknownTransactionId_Returns404ProblemDetails()
        {
            var response = await _client.DeleteAsync($"/api/transactions/{Guid.NewGuid()}");

            response.StatusCode.Should().Be(HttpStatusCode.NotFound);

            var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>();
            problem!.Status.Should().Be((int)HttpStatusCode.NotFound);
        }

        [Fact]
        public async Task Categorize_WithValidCategoryId_Returns204()
        {
            var accountId = await CreatePersistedAccountAsync();
            var transactionId = await CreatePersistedTransactionAsync(accountId);
            var categoryId = await CreatePersistedCategoryAsync();

            var response = await _client.PutAsJsonAsync(
                $"/api/transactions/{transactionId}/category", new CategorizeTransactionRequest(categoryId));

            response.StatusCode.Should().Be(HttpStatusCode.NoContent);
        }

        [Fact]
        public async Task Categorize_WithNullCategoryId_ClearsCategoryAndReturns204()
        {
            var accountId = await CreatePersistedAccountAsync();
            var transactionId = await CreatePersistedTransactionAsync(accountId);
            var categoryId = await CreatePersistedCategoryAsync();
            await _client.PutAsJsonAsync($"/api/transactions/{transactionId}/category", new CategorizeTransactionRequest(categoryId));

            var response = await _client.PutAsJsonAsync(
                $"/api/transactions/{transactionId}/category", new CategorizeTransactionRequest(null));

            response.StatusCode.Should().Be(HttpStatusCode.NoContent);
        }

        [Fact]
        public async Task Categorize_WithUnknownTransactionId_Returns404ProblemDetails()
        {
            var categoryId = await CreatePersistedCategoryAsync();

            var response = await _client.PutAsJsonAsync(
                $"/api/transactions/{Guid.NewGuid()}/category", new CategorizeTransactionRequest(categoryId));

            response.StatusCode.Should().Be(HttpStatusCode.NotFound);

            var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>();
            problem!.Status.Should().Be((int)HttpStatusCode.NotFound);
        }

        [Fact]
        public async Task Categorize_WithUnknownCategoryId_Returns404ProblemDetails()
        {
            var accountId = await CreatePersistedAccountAsync();
            var transactionId = await CreatePersistedTransactionAsync(accountId);

            var response = await _client.PutAsJsonAsync(
                $"/api/transactions/{transactionId}/category", new CategorizeTransactionRequest(Guid.NewGuid()));

            response.StatusCode.Should().Be(HttpStatusCode.NotFound);

            var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>();
            problem!.Status.Should().Be((int)HttpStatusCode.NotFound);
        }

        [Fact]
        public async Task GetTransactions_ForAccountWithTransactions_ReturnsThemMostRecentFirst()
        {
            var accountId = await CreatePersistedAccountAsync();
            var today = DateOnly.FromDateTime(DateTime.UtcNow);
            await CreatePersistedTransactionAsync(accountId, today.AddDays(-5));
            await CreatePersistedTransactionAsync(accountId, today);

            var response = await _client.GetAsync($"/api/transactions?accountId={accountId}");

            response.StatusCode.Should().Be(HttpStatusCode.OK);

            var results = await response.Content.ReadFromJsonAsync<List<TransactionResponse>>(JsonOptions);
            results.Should().HaveCount(2);
            results![0].OccurredOn.Should().Be(today);
            results[1].OccurredOn.Should().Be(today.AddDays(-5));
        }

        [Fact]
        public async Task GetTransactions_WithDateRange_ExcludesTransactionsOutsideIt()
        {
            var accountId = await CreatePersistedAccountAsync();
            var today = DateOnly.FromDateTime(DateTime.UtcNow);
            await CreatePersistedTransactionAsync(accountId, today.AddDays(-30));
            await CreatePersistedTransactionAsync(accountId, today);

            var response = await _client.GetAsync(
                $"/api/transactions?accountId={accountId}&from={today.AddDays(-1):yyyy-MM-dd}&to={today:yyyy-MM-dd}");

            response.StatusCode.Should().Be(HttpStatusCode.OK);

            var results = await response.Content.ReadFromJsonAsync<List<TransactionResponse>>(JsonOptions);
            results.Should().ContainSingle();
            results![0].OccurredOn.Should().Be(today);
        }

        [Fact]
        public async Task GetTransactions_WithUnknownAccountId_Returns404ProblemDetails()
        {
            var response = await _client.GetAsync($"/api/transactions?accountId={Guid.NewGuid()}");

            response.StatusCode.Should().Be(HttpStatusCode.NotFound);

            var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>();
            problem!.Status.Should().Be((int)HttpStatusCode.NotFound);
        }

        [Fact]
        public async Task GetTransactions_WithoutAccountId_Returns400ProblemDetails()
        {
            var response = await _client.GetAsync("/api/transactions");

            response.StatusCode.Should().Be(HttpStatusCode.BadRequest);

            var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>();
            problem!.Status.Should().Be((int)HttpStatusCode.BadRequest);
        }

        [Fact]
        public async Task GetSpendingSummary_GroupsExpensesByCategory()
        {
            var accountId = await CreatePersistedAccountAsync();
            var categoryId = await CreatePersistedCategoryAsync();
            var today = DateOnly.FromDateTime(DateTime.UtcNow);

            var firstId = await CreatePersistedTransactionAsync(accountId, today);
            await _client.PutAsJsonAsync($"/api/transactions/{firstId}/category", new CategorizeTransactionRequest(categoryId));
            var secondId = await CreatePersistedTransactionAsync(accountId, today);
            await _client.PutAsJsonAsync($"/api/transactions/{secondId}/category", new CategorizeTransactionRequest(categoryId));

            var response = await _client.GetAsync(
                $"/api/transactions/spending-summary?accountId={accountId}&year={today.Year}&month={today.Month}");

            response.StatusCode.Should().Be(HttpStatusCode.OK);

            var results = await response.Content.ReadFromJsonAsync<List<CategorySpendingResponse>>();
            results.Should().ContainSingle();
            results![0].CategoryId.Should().Be(categoryId);
            results[0].Total.Should().Be(50.00m);
            results[0].Currency.Should().Be("USD");
        }

        [Fact]
        public async Task GetSpendingSummary_IncludesUncategorizedSpendingWithNullCategoryId()
        {
            var accountId = await CreatePersistedAccountAsync();
            var today = DateOnly.FromDateTime(DateTime.UtcNow);
            await CreatePersistedTransactionAsync(accountId, today);

            var response = await _client.GetAsync(
                $"/api/transactions/spending-summary?accountId={accountId}&year={today.Year}&month={today.Month}");

            response.StatusCode.Should().Be(HttpStatusCode.OK);

            var results = await response.Content.ReadFromJsonAsync<List<CategorySpendingResponse>>();
            results.Should().ContainSingle();
            results![0].CategoryId.Should().BeNull();
        }

        [Fact]
        public async Task GetSpendingSummary_ExcludesIncomeTransactions()
        {
            var accountId = await CreatePersistedAccountAsync();
            var today = DateOnly.FromDateTime(DateTime.UtcNow);
            await _client.PostAsJsonAsync(
                "/api/transactions", new AddTransactionRequest(accountId, 1000m, TransactionType.Income, "Paycheck", today));

            var response = await _client.GetAsync(
                $"/api/transactions/spending-summary?accountId={accountId}&year={today.Year}&month={today.Month}");

            response.StatusCode.Should().Be(HttpStatusCode.OK);

            var results = await response.Content.ReadFromJsonAsync<List<CategorySpendingResponse>>();
            results.Should().BeEmpty();
        }

        [Fact]
        public async Task GetSpendingSummary_ExcludesTransactionsOutsideThePeriod()
        {
            var accountId = await CreatePersistedAccountAsync();
            var today = DateOnly.FromDateTime(DateTime.UtcNow);
            var lastMonth = today.AddMonths(-1);
            await CreatePersistedTransactionAsync(accountId, lastMonth);

            var response = await _client.GetAsync(
                $"/api/transactions/spending-summary?accountId={accountId}&year={today.Year}&month={today.Month}");

            response.StatusCode.Should().Be(HttpStatusCode.OK);

            var results = await response.Content.ReadFromJsonAsync<List<CategorySpendingResponse>>();
            results.Should().BeEmpty();
        }

        [Fact]
        public async Task GetSpendingSummary_WithUnknownAccountId_Returns404ProblemDetails()
        {
            var today = DateOnly.FromDateTime(DateTime.UtcNow);

            var response = await _client.GetAsync(
                $"/api/transactions/spending-summary?accountId={Guid.NewGuid()}&year={today.Year}&month={today.Month}");

            response.StatusCode.Should().Be(HttpStatusCode.NotFound);

            var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>();
            problem!.Status.Should().Be((int)HttpStatusCode.NotFound);
        }

        [Fact]
        public async Task GetSpendingSummary_WithInvalidMonth_Returns400ProblemDetails()
        {
            var accountId = await CreatePersistedAccountAsync();

            var response = await _client.GetAsync(
                $"/api/transactions/spending-summary?accountId={accountId}&year=2026&month=13");

            response.StatusCode.Should().Be(HttpStatusCode.BadRequest);

            var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>();
            problem!.Status.Should().Be((int)HttpStatusCode.BadRequest);
        }
    }
}
