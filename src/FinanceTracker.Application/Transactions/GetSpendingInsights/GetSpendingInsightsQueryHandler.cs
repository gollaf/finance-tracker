using System.Globalization;
using FinanceTracker.Application.Accounts;
using FinanceTracker.Application.Categories;
using FinanceTracker.Application.Common;
using FinanceTracker.Domain.Budgets;
using FinanceTracker.Domain.Common;
using FinanceTracker.Domain.Transactions;
using MediatR;

namespace FinanceTracker.Application.Transactions.GetSpendingInsights
{
    /// <summary>
    /// Computes this-month-vs-prior-3-month-average spending per Category,
    /// then asks IInsightsGenerator to describe it in plain language. Every
    /// number in the result comes from this handler, never from the AI --
    /// see docs/adr/0010-ai-insights-provider-and-integration-design.md. An
    /// IInsightsGenerator failure degrades Narrative to a templated fallback
    /// built from the same Trends; it never fails the query.
    /// </summary>
    public sealed class GetSpendingInsightsQueryHandler
        : IRequestHandler<GetSpendingInsightsQuery, Result<SpendingInsightsDto>>
    {
        private readonly IAccountRepository _accountRepository;
        private readonly ITransactionRepository _transactionRepository;
        private readonly ICategoryRepository _categoryRepository;
        private readonly IInsightsGenerator _insightsGenerator;

        public GetSpendingInsightsQueryHandler(
            IAccountRepository accountRepository,
            ITransactionRepository transactionRepository,
            ICategoryRepository categoryRepository,
            IInsightsGenerator insightsGenerator)
        {
            _accountRepository = accountRepository;
            _transactionRepository = transactionRepository;
            _categoryRepository = categoryRepository;
            _insightsGenerator = insightsGenerator;
        }

        public async Task<Result<SpendingInsightsDto>> Handle(
            GetSpendingInsightsQuery request, CancellationToken cancellationToken)
        {
            var account = await _accountRepository.GetByIdAsync(request.AccountId, cancellationToken);

            if (account is null)
            {
                return Result.Failure<SpendingInsightsDto>(Error.NotFound(
                    "Account.NotFound", $"No account found with id '{request.AccountId}'."));
            }

            var period = BudgetPeriod.Create(request.Year, request.Month);
            var currency = account.Currency;

            // BudgetPeriod only knows "does this date fall in this one
            // calendar month" (Contains) -- the prior-3-months window below
            // spans three, so it's built directly from DateOnly boundaries
            // instead of three separate BudgetPeriod.Contains checks.
            var currentMonthStart = new DateOnly(request.Year, request.Month, 1);
            var currentMonthEndExclusive = currentMonthStart.AddMonths(1);
            var priorWindowStart = currentMonthStart.AddMonths(-3);

            var transactions = await _transactionRepository.GetByAccountIdAsync(request.AccountId, cancellationToken);

            // Grouped into a list of (CategoryId?, Money) pairs, not a
            // Dictionary<CategoryId?, Money> -- Dictionary rejects a null
            // key even when TKey is a nullable value type like CategoryId?
            // (its null-check runs before any comparer is consulted), and
            // an uncategorized Transaction's CategoryId is exactly that
            // null key. Category counts here are always small, so a linear
            // FirstOrDefault lookup below costs nothing that matters.
            var currentMonthByCategory = transactions
                .Where(t => t.Type == TransactionType.Expense)
                .Where(t => t.OccurredOn >= currentMonthStart && t.OccurredOn < currentMonthEndExclusive)
                .GroupBy(t => t.CategoryId)
                .Select(g => (CategoryId: g.Key, Total: g.Aggregate(Money.Zero(currency), (total, t) => total + t.Amount)))
                .ToList();

            // Summed across all three prior months, then divided by 3 below
            // -- a "3-month average," not a 3-month total.
            var priorWindowByCategory = transactions
                .Where(t => t.Type == TransactionType.Expense)
                .Where(t => t.OccurredOn >= priorWindowStart && t.OccurredOn < currentMonthStart)
                .GroupBy(t => t.CategoryId)
                .Select(g => (CategoryId: g.Key, Total: g.Aggregate(Money.Zero(currency), (total, t) => total + t.Amount)))
                .ToList();

            var categoryIds = currentMonthByCategory.Select(c => c.CategoryId)
                .Concat(priorWindowByCategory.Select(c => c.CategoryId))
                .Distinct()
                .ToList();

            var trends = new List<CategoryTrendDto>(categoryIds.Count);

            foreach (var categoryId in categoryIds)
            {
                var categoryName = await ResolveCategoryNameAsync(categoryId, cancellationToken);

                var currentTotal = TotalFor(categoryId, currentMonthByCategory, currency);
                var priorTotal = TotalFor(categoryId, priorWindowByCategory, currency);
                var priorAverage = Money.Create(priorTotal.Amount / 3m, currency);

                var percentChange = priorAverage.IsZero
                    ? (decimal?)null
                    : Math.Round((currentTotal.Amount - priorAverage.Amount) / priorAverage.Amount * 100m, 1);

                trends.Add(new CategoryTrendDto(categoryId, categoryName, currentTotal, priorAverage, percentChange));
            }

            trends = trends.OrderByDescending(t => t.CurrentMonthTotal.Amount).ToList();

            string narrative;
            bool narrativeGeneratedByAi;

            if (trends.Count == 0)
            {
                // Nothing to summarize -- skip the AI call entirely rather
                // than sending an empty prompt (see ADR 0010, Consequences).
                narrative = $"No expense activity found for {FormatPeriod(period)} or the three months " +
                    "before it, so there isn't enough history yet to generate spending insights.";
                narrativeGeneratedByAi = false;
            }
            else
            {
                var aiResult = await _insightsGenerator.GenerateAsync(
                    new InsightsGenerationRequest(currency, period, trends), cancellationToken);

                if (aiResult.IsSuccess)
                {
                    narrative = aiResult.Value;
                    narrativeGeneratedByAi = true;
                }
                else
                {
                    narrative = BuildFallbackNarrative(period, trends);
                    narrativeGeneratedByAi = false;
                }
            }

            return Result.Success(new SpendingInsightsDto(trends, narrative, narrativeGeneratedByAi));
        }

        private static Money TotalFor(
            CategoryId? categoryId, List<(CategoryId? CategoryId, Money Total)> totalsByCategory, string currency)
        {
            foreach (var (candidateCategoryId, total) in totalsByCategory)
            {
                if (candidateCategoryId == categoryId)
                    return total;
            }

            return Money.Zero(currency);
        }

        private async Task<string> ResolveCategoryNameAsync(CategoryId? categoryId, CancellationToken cancellationToken)
        {
            if (categoryId is null)
                return "Uncategorized";

            var category = await _categoryRepository.GetByIdAsync(categoryId.Value, cancellationToken);

            // Defensive, not expected in practice: ADR 0005's Restrict
            // foreign key means a Category referenced by a Transaction can't
            // actually be deleted.
            return category?.Name ?? "Unknown Category";
        }

        private static string BuildFallbackNarrative(BudgetPeriod period, IReadOnlyList<CategoryTrendDto> trends)
        {
            var sentences = trends.Select(t =>
            {
                var changeClause = t.PercentChange is { } change
                    ? $" ({(change >= 0 ? "+" : string.Empty)}{change.ToString(CultureInfo.InvariantCulture)}% vs average)"
                    : string.Empty;

                return $"{t.CategoryName}: {t.CurrentMonthTotal} this month, " +
                    $"{t.PriorAverageTotal} average over the prior 3 months{changeClause}.";
            });

            return $"Spending by category for {FormatPeriod(period)}: {string.Join(" ", sentences)}";
        }

        private static string FormatPeriod(BudgetPeriod period) =>
            new DateOnly(period.Year, period.Month, 1).ToString("MMMM yyyy", CultureInfo.InvariantCulture);
    }
}
