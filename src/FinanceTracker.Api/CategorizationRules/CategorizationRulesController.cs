using FinanceTracker.Api.Common;
using FinanceTracker.Application.Categorization.CreateCategorizationRule;
using FinanceTracker.Domain.Common;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace FinanceTracker.Api.CategorizationRules
{
    // Route uses a kebab-case segment ("categorization-rules") rather than
    // AccountsController/CategoriesController's plain lowercased-word
    // style -- those are both single words, so there was no separator to
    // decide on yet. Kebab-case is the more standard REST convention for a
    // multi-word resource name, and this is the first one.
    [ApiController]
    [Route("api/categorization-rules")]
    public sealed class CategorizationRulesController : ControllerBase
    {
        private readonly ISender _sender;

        public CategorizationRulesController(ISender sender)
        {
            _sender = sender;
        }

        [HttpPost]
        public async Task<IActionResult> Create(CreateCategorizationRuleRequest request, CancellationToken cancellationToken)
        {
            var command = new CreateCategorizationRuleCommand(
                request.Pattern, new CategoryId(request.CategoryId), request.Priority);
            var result = await _sender.Send(command, cancellationToken);

            if (result.IsFailure)
                return result.ToActionResult();

            var ruleId = result.Value.Value;
            var response = new CreateCategorizationRuleResponse(ruleId);

            return Created($"/api/categorization-rules/{ruleId}", response);
        }
    }
}
