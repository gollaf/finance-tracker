using FinanceTracker.Api.Common;
using FinanceTracker.Application.Budgets.CreateBudget;
using FinanceTracker.Application.Budgets.GetBudgetStatus;
using FinanceTracker.Application.Budgets.UpdateBudget;
using FinanceTracker.Domain.Common;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace FinanceTracker.Api.Budgets
{
    [ApiController]
    [Route("api/budgets")]
    public sealed class BudgetsController : ControllerBase
    {
        private readonly ISender _sender;

        public BudgetsController(ISender sender)
        {
            _sender = sender;
        }

        [HttpPost]
        public async Task<IActionResult> Create(CreateBudgetRequest request, CancellationToken cancellationToken)
        {
            var command = new CreateBudgetCommand(
                new CategoryId(request.CategoryId), request.Year, request.Month, request.LimitAmount, request.Currency);
            var result = await _sender.Send(command, cancellationToken);

            if (result.IsFailure)
                return result.ToActionResult();

            var budgetId = result.Value.Value;
            var response = new CreateBudgetResponse(budgetId);

            return Created($"/api/budgets/{budgetId}", response);
        }

        // UpdateBudget only ever returns a plain Result (no value to hand
        // back) and there's nothing sensible to put in a body -- the
        // caller already knows the LimitAmount it just sent -- so success
        // here is 204 No Content, the conventional shape for "the update
        // happened, there's nothing to return."
        [HttpPut("{id:guid}")]
        public async Task<IActionResult> Update(Guid id, UpdateBudgetRequest request, CancellationToken cancellationToken)
        {
            var command = new UpdateBudgetCommand(new BudgetId(id), request.LimitAmount);
            var result = await _sender.Send(command, cancellationToken);

            if (result.IsFailure)
                return result.ToActionResult();

            return NoContent();
        }

        [HttpGet("{id:guid}/status")]
        public async Task<IActionResult> GetStatus(Guid id, CancellationToken cancellationToken)
        {
            var query = new GetBudgetStatusQuery(new BudgetId(id));
            var result = await _sender.Send(query, cancellationToken);

            if (result.IsFailure)
                return result.ToActionResult();

            var status = result.Value;
            var response = new BudgetStatusResponse(
                status.BudgetId.Value,
                status.CategoryId.Value,
                status.Period.Year,
                status.Period.Month,
                status.LimitAmount.Amount,
                status.ActualSpending.Amount,
                status.Remaining.Amount,
                status.LimitAmount.Currency,
                status.IsOverBudget);

            return Ok(response);
        }
    }
}
