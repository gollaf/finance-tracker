using FinanceTracker.Api.Common;
using FinanceTracker.Application.Transactions.AddTransaction;
using FinanceTracker.Application.Transactions.CategorizeTransaction;
using FinanceTracker.Application.Transactions.DeleteTransaction;
using FinanceTracker.Application.Transactions.GetTransactions;
using FinanceTracker.Application.Transactions.UpdateTransaction;
using FinanceTracker.Domain.Common;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace FinanceTracker.Api.Transactions
{
    /// <summary>
    /// Covers Transaction's core lifecycle (Add, Update, Delete), assigning
    /// a Category, and listing an Account's Transactions. GetSpendingSummary
    /// lands here in a later piece.
    /// </summary>
    [ApiController]
    [Route("api/transactions")]
    public sealed class TransactionsController : ControllerBase
    {
        private readonly ISender _sender;

        public TransactionsController(ISender sender)
        {
            _sender = sender;
        }

        [HttpPost]
        public async Task<IActionResult> Add(AddTransactionRequest request, CancellationToken cancellationToken)
        {
            var command = new AddTransactionCommand(
                new AccountId(request.AccountId), request.Amount, request.Type, request.Description, request.OccurredOn);
            var result = await _sender.Send(command, cancellationToken);

            if (result.IsFailure)
                return result.ToActionResult();

            var transactionId = result.Value.Value;
            var response = new AddTransactionResponse(transactionId);

            return Created($"/api/transactions/{transactionId}", response);
        }

        [HttpPut("{id:guid}")]
        public async Task<IActionResult> Update(Guid id, UpdateTransactionRequest request, CancellationToken cancellationToken)
        {
            var command = new UpdateTransactionCommand(
                new TransactionId(id), request.Amount, request.Description, request.OccurredOn);
            var result = await _sender.Send(command, cancellationToken);

            if (result.IsFailure)
                return result.ToActionResult();

            return NoContent();
        }

        // The first DELETE action in the API: no request body, no response
        // body -- just "this either happened (204) or the id did not
        // resolve to anything (404)."
        [HttpDelete("{id:guid}")]
        public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
        {
            var command = new DeleteTransactionCommand(new TransactionId(id));
            var result = await _sender.Send(command, cancellationToken);

            if (result.IsFailure)
                return result.ToActionResult();

            return NoContent();
        }

        // Modeled as replacing the value of a "category" sub-resource on the
        // Transaction, rather than a partial PATCH of the whole Transaction --
        // it's the only field this action ever touches, and PUT's "set this
        // to exactly this value" semantics fit a nullable CategoryId well:
        // an absent/null body value means "clear it."
        [HttpPut("{id:guid}/category")]
        public async Task<IActionResult> Categorize(Guid id, CategorizeTransactionRequest request, CancellationToken cancellationToken)
        {
            var categoryId = request.CategoryId is { } rawCategoryId ? new CategoryId(rawCategoryId) : (CategoryId?)null;
            var command = new CategorizeTransactionCommand(new TransactionId(id), categoryId);
            var result = await _sender.Send(command, cancellationToken);

            if (result.IsFailure)
                return result.ToActionResult();

            return NoContent();
        }

        // AccountId, From and To all come from the query string, not the
        // route or a body -- GET requests don't have a body, and AccountId
        // here is a filter on the Transaction list, not a parent resource
        // in the URL (Transaction is its own top-level aggregate/controller,
        // per ADR 0004 -- this deliberately isn't nested under /api/accounts).
        [HttpGet]
        public async Task<IActionResult> GetTransactions(
            [FromQuery] Guid accountId, [FromQuery] DateOnly? from, [FromQuery] DateOnly? to, CancellationToken cancellationToken)
        {
            var query = new GetTransactionsQuery(new AccountId(accountId), from, to);
            var result = await _sender.Send(query, cancellationToken);

            if (result.IsFailure)
                return result.ToActionResult();

            var response = result.Value
                .Select(t => new TransactionResponse(
                    t.Id.Value,
                    t.AccountId.Value,
                    t.CategoryId?.Value,
                    t.Amount.Amount,
                    t.Amount.Currency,
                    t.Type,
                    t.Description,
                    t.OccurredOn))
                .ToList();

            return Ok(response);
        }
    }
}
