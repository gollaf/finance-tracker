using FinanceTracker.Application.Common;
using FinanceTracker.Application.Transactions;
using FinanceTracker.Application.Transactions.GetSpendingInsights;
using FinanceTracker.Domain.Budgets;
using FinanceTracker.Infrastructure.Ai;
using FluentAssertions;

namespace FinanceTracker.Infrastructure.IntegrationTests.Ai
{
    public sealed class NotConfiguredInsightsGeneratorTests
    {
        [Fact]
        public async Task GenerateAsync_AlwaysReturnsFailure()
        {
            var generator = new NotConfiguredInsightsGenerator();
            var request = new InsightsGenerationRequest(
                "USD", BudgetPeriod.Create(2026, 6), Array.Empty<CategoryTrendDto>());

            var result = await generator.GenerateAsync(request, CancellationToken.None);

            result.IsFailure.Should().BeTrue();
            result.Error.Code.Should().Be("InsightsGenerator.NotConfigured");
        }
    }
}
