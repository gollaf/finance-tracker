using System.Net;
using System.Net.Http.Json;
using FinanceTracker.Api.Accounts;
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

        private async Task<Guid> CreatePersistedAccountAsync()
        {
            var response = await _client.PostAsJsonAsync(
                "/api/accounts", new CreateAccountRequest("Checking", AccountType.Checking, "USD"));
            var created = await response.Content.ReadFromJsonAsync<CreateAccountResponse>();
            return created!.Id;
        }

        private async Task<Guid> CreatePersistedTransactionAsync(Guid accountId)
        {
            var response = await _client.PostAsJsonAsync(
                "/api/transactions",
                new AddTransactionRequest(accountId, 25.00m, TransactionType.Expense, "Coffee", DateOnly.FromDateTime(DateTime.UtcNow)));
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
    }
}
