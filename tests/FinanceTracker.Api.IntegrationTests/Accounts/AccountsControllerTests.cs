using System.Net;
using System.Net.Http.Json;
using FinanceTracker.Api.Accounts;
using FinanceTracker.Domain.Accounts;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc;

namespace FinanceTracker.Api.IntegrationTests.Accounts
{
    public sealed class AccountsControllerTests : IClassFixture<CustomWebApplicationFactory>
    {
        private readonly HttpClient _client;

        public AccountsControllerTests(CustomWebApplicationFactory factory)
        {
            _client = factory.CreateClient();
        }

        [Fact]
        public async Task Create_WithValidRequest_Returns201WithLocationAndId()
        {
            var request = new CreateAccountRequest("Checking", AccountType.Checking, "USD");

            var response = await _client.PostAsJsonAsync("/api/accounts", request);

            response.StatusCode.Should().Be(HttpStatusCode.Created);
            response.Headers.Location.Should().NotBeNull();

            var body = await response.Content.ReadFromJsonAsync<CreateAccountResponse>();
            body!.Id.Should().NotBeEmpty();
        }

        [Fact]
        public async Task Create_WithMissingName_Returns400ProblemDetails()
        {
            var request = new CreateAccountRequest("", AccountType.Checking, "USD");

            var response = await _client.PostAsJsonAsync("/api/accounts", request);

            response.StatusCode.Should().Be(HttpStatusCode.BadRequest);

            var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>();
            problem!.Status.Should().Be((int)HttpStatusCode.BadRequest);
        }

        [Fact]
        public async Task GetBalance_ForNewlyCreatedAccount_ReturnsZero()
        {
            var createRequest = new CreateAccountRequest("Checking", AccountType.Checking, "USD");
            var createResponse = await _client.PostAsJsonAsync("/api/accounts", createRequest);
            var created = await createResponse.Content.ReadFromJsonAsync<CreateAccountResponse>();

            var response = await _client.GetAsync($"/api/accounts/{created!.Id}/balance");

            response.StatusCode.Should().Be(HttpStatusCode.OK);

            var balance = await response.Content.ReadFromJsonAsync<AccountBalanceResponse>();
            balance!.Amount.Should().Be(0m);
            balance.Currency.Should().Be("USD");
        }

        [Fact]
        public async Task GetBalance_WithUnknownAccountId_Returns404ProblemDetails()
        {
            var response = await _client.GetAsync($"/api/accounts/{Guid.NewGuid()}/balance");

            response.StatusCode.Should().Be(HttpStatusCode.NotFound);

            var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>();
            problem!.Status.Should().Be((int)HttpStatusCode.NotFound);
        }
    }
}
