using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using FinanceTracker.Api.Accounts;
using FinanceTracker.Api.Transactions;
using FinanceTracker.Domain.Accounts;
using FinanceTracker.Domain.Transactions;
using FluentAssertions;

namespace FinanceTracker.Api.IntegrationTests.EndToEnd
{
    /// <summary>
    /// A full user flow through real HTTP and Postgres, to catch what
    /// per-endpoint tests miss: a wrong route, a currency mismatch, a filter
    /// that drops the wrong rows.
    /// </summary>
    public sealed class AccountLifecycleTests : IClassFixture<CustomWebApplicationFactory>
    {
        private readonly HttpClient _client;

        public AccountLifecycleTests(CustomWebApplicationFactory factory)
        {
            _client = factory.CreateClient();
        }

        // The server sends enums as strings (see TransactionsControllerTests).
        private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
        {
            Converters = { new JsonStringEnumConverter() },
        };

        [Fact]
        public async Task CreateAccount_AddTransactions_DeleteOne_BalanceAndListReflectEachStep()
        {
            // 1. Create the account.
            var createAccountResponse = await _client.PostAsJsonAsync(
                "/api/accounts", new CreateAccountRequest("Checking", AccountType.Checking, "USD"));
            createAccountResponse.StatusCode.Should().Be(HttpStatusCode.Created);
            var account = await createAccountResponse.Content.ReadFromJsonAsync<CreateAccountResponse>();
            var accountId = account!.Id;

            // 2. A brand-new account has a zero balance.
            var initialBalance = await GetBalanceAsync(accountId);
            initialBalance.Amount.Should().Be(0m);
            initialBalance.Currency.Should().Be("USD");

            // 3. Add income and an expense.
            var today = DateOnly.FromDateTime(DateTime.UtcNow);
            var incomeResponse = await _client.PostAsJsonAsync(
                "/api/transactions",
                new AddTransactionRequest(accountId, 200m, TransactionType.Income, "Paycheck", today));
            incomeResponse.StatusCode.Should().Be(HttpStatusCode.Created);

            var expenseResponse = await _client.PostAsJsonAsync(
                "/api/transactions",
                new AddTransactionRequest(accountId, 50m, TransactionType.Expense, "Groceries", today));
            expenseResponse.StatusCode.Should().Be(HttpStatusCode.Created);
            var expense = await expenseResponse.Content.ReadFromJsonAsync<AddTransactionResponse>();

            // 4. Balance nets both: 200 income - 50 expense = 150.
            var balanceAfterBoth = await GetBalanceAsync(accountId);
            balanceAfterBoth.Amount.Should().Be(150m);

            // 5. Both show up when listing the account's transactions.
            var listResponse = await _client.GetAsync($"/api/transactions?accountId={accountId}");
            var transactions = await listResponse.Content.ReadFromJsonAsync<List<TransactionResponse>>(JsonOptions);
            transactions.Should().HaveCount(2);

            // 6. Delete the expense; the balance goes back to just the income.
            var deleteResponse = await _client.DeleteAsync($"/api/transactions/{expense!.Id}");
            deleteResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);

            var finalBalance = await GetBalanceAsync(accountId);
            finalBalance.Amount.Should().Be(200m);

            var finalList = await _client.GetAsync($"/api/transactions?accountId={accountId}");
            var finalTransactions = await finalList.Content.ReadFromJsonAsync<List<TransactionResponse>>(JsonOptions);
            finalTransactions.Should().ContainSingle();
        }

        private async Task<AccountBalanceResponse> GetBalanceAsync(Guid accountId)
        {
            var response = await _client.GetAsync($"/api/accounts/{accountId}/balance");
            response.StatusCode.Should().Be(HttpStatusCode.OK);
            var balance = await response.Content.ReadFromJsonAsync<AccountBalanceResponse>();
            return balance!;
        }
    }
}
