using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using FinanceTracker.Api.Accounts;
using FinanceTracker.Api.Imports;
using FinanceTracker.Api.Transactions;
using FinanceTracker.Domain.Accounts;
using FinanceTracker.Domain.Transactions;
using FluentAssertions;

namespace FinanceTracker.Api.IntegrationTests.Ownership
{
    /// <summary>
    /// Two signed-in users: the factory's test user owns the data, and a
    /// second user tries to reach it. Someone else's account, transaction or
    /// import job answers 404, exactly as if it didn't exist.
    /// </summary>
    public sealed class AccountOwnershipTests : IClassFixture<CustomWebApplicationFactory>, IAsyncLifetime
    {
        private static readonly DateOnly Today = DateOnly.FromDateTime(DateTime.UtcNow);

        // The server sends enums as strings, which ReadFromJsonAsync's
        // default options can't read.
        private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
        {
            Converters = { new JsonStringEnumConverter() },
        };

        private readonly CustomWebApplicationFactory _factory;
        private readonly HttpClient _owner;
        private HttpClient _stranger = null!;

        public AccountOwnershipTests(CustomWebApplicationFactory factory)
        {
            _factory = factory;
            _owner = factory.CreateClient();
        }

        public async Task InitializeAsync() => _stranger = await _factory.CreateClientForNewUserAsync();

        public Task DisposeAsync() => Task.CompletedTask;

        private async Task<Guid> CreateAccountAsOwnerAsync()
        {
            var response = await _owner.PostAsJsonAsync(
                "/api/accounts", new CreateAccountRequest("Checking", AccountType.Checking, "USD"));
            response.EnsureSuccessStatusCode();
            return (await response.Content.ReadFromJsonAsync<CreateAccountResponse>())!.Id;
        }

        private async Task<Guid> AddTransactionAsOwnerAsync(Guid accountId)
        {
            var response = await _owner.PostAsJsonAsync(
                "/api/transactions", new AddTransactionRequest(accountId, 10m, TransactionType.Expense, "Coffee", Today));
            response.EnsureSuccessStatusCode();
            return (await response.Content.ReadFromJsonAsync<AddTransactionResponse>())!.Id;
        }

        [Fact]
        public async Task AnotherUsersAccountBalance_Returns404()
        {
            var accountId = await CreateAccountAsOwnerAsync();

            var asStranger = await _stranger.GetAsync($"/api/accounts/{accountId}/balance");
            var asOwner = await _owner.GetAsync($"/api/accounts/{accountId}/balance");

            asStranger.StatusCode.Should().Be(HttpStatusCode.NotFound);
            asOwner.StatusCode.Should().Be(HttpStatusCode.OK);
        }

        [Fact]
        public async Task AnotherUsersTransactions_CannotBeListed()
        {
            var accountId = await CreateAccountAsOwnerAsync();
            await AddTransactionAsOwnerAsync(accountId);

            var response = await _stranger.GetAsync($"/api/transactions?accountId={accountId}");

            response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        }

        [Fact]
        public async Task AddingATransactionToAnotherUsersAccount_Returns404()
        {
            var accountId = await CreateAccountAsOwnerAsync();

            var response = await _stranger.PostAsJsonAsync(
                "/api/transactions", new AddTransactionRequest(accountId, 99m, TransactionType.Expense, "Not mine", Today));

            response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        }

        [Fact]
        public async Task AnotherUsersTransaction_CannotBeChangedOrDeleted()
        {
            var accountId = await CreateAccountAsOwnerAsync();
            var transactionId = await AddTransactionAsOwnerAsync(accountId);

            var update = await _stranger.PutAsJsonAsync(
                $"/api/transactions/{transactionId}", new UpdateTransactionRequest(999m, "Changed", Today));
            var delete = await _stranger.DeleteAsync($"/api/transactions/{transactionId}");

            update.StatusCode.Should().Be(HttpStatusCode.NotFound);
            delete.StatusCode.Should().Be(HttpStatusCode.NotFound);

            var ownersView = await _owner.GetFromJsonAsync<List<TransactionResponse>>(
                $"/api/transactions?accountId={accountId}", JsonOptions);
            ownersView.Should().ContainSingle(t => t.Id == transactionId && t.Amount == 10m && t.Description == "Coffee");
        }

        [Fact]
        public async Task AnotherUsersImportJob_Returns404()
        {
            var accountId = await CreateAccountAsOwnerAsync();
            using var content = new MultipartFormDataContent
            {
                { new StringContent(accountId.ToString()), "AccountId" },
            };
            var file = new ByteArrayContent(Encoding.UTF8.GetBytes(
                "Amount,Type,Description,OccurredOn\n25.00,Expense,Coffee,2026-01-10\n"));
            file.Headers.ContentType = new MediaTypeHeaderValue("text/csv");
            content.Add(file, "File", "transactions.csv");

            var started = await _owner.PostAsync("/api/transactions/import", content);
            started.StatusCode.Should().Be(HttpStatusCode.Accepted);
            var importJobId = (await started.Content.ReadFromJsonAsync<StartImportResponse>())!.ImportJobId;

            var asStranger = await _stranger.GetAsync($"/api/imports/{importJobId}");
            var asOwner = await _owner.GetAsync($"/api/imports/{importJobId}");

            asStranger.StatusCode.Should().Be(HttpStatusCode.NotFound);
            asOwner.StatusCode.Should().Be(HttpStatusCode.OK);
        }

        [Fact]
        public async Task ImportingIntoAnotherUsersAccount_Returns404()
        {
            var accountId = await CreateAccountAsOwnerAsync();
            using var content = new MultipartFormDataContent
            {
                { new StringContent(accountId.ToString()), "AccountId" },
            };
            var file = new ByteArrayContent(Encoding.UTF8.GetBytes(
                "Amount,Type,Description,OccurredOn\n25.00,Expense,Coffee,2026-01-10\n"));
            file.Headers.ContentType = new MediaTypeHeaderValue("text/csv");
            content.Add(file, "File", "transactions.csv");

            var response = await _stranger.PostAsync("/api/transactions/import", content);

            response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        }
    }
}
