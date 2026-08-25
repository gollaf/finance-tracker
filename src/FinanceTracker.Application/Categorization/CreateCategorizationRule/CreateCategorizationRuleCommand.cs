using FinanceTracker.Application.Common;
using FinanceTracker.Domain.Common;
using MediatR;

namespace FinanceTracker.Application.Categorization.CreateCategorizationRule
{
    public sealed record CreateCategorizationRuleCommand(string Pattern, CategoryId CategoryId, int Priority)
        : IRequest<Result<CategorizationRuleId>>;
}
