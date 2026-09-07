namespace FinanceTracker.Api.CategorizationRules
{
    public sealed record CreateCategorizationRuleRequest(string Pattern, Guid CategoryId, int Priority);
}
