using FinanceTracker.Application.Common;
using FinanceTracker.Domain.Common;
using MediatR;

namespace FinanceTracker.Application.Transactions.GetSpendingInsights
{
    /// <summary>
    /// Compares one Account's Expense spending by Category for one month
    /// against the average of the same Category across the three preceding
    /// calendar months, and asks an IInsightsGenerator to describe the
    /// comparison in plain language. See
    /// docs/adr/0010-ai-insights-provider-and-integration-design.md for why
    /// the AI is only ever handed numbers this query already computed.
    /// </summary>
    public sealed record GetSpendingInsightsQuery(AccountId AccountId, int Year, int Month)
        : IRequest<Result<SpendingInsightsDto>>;
}
