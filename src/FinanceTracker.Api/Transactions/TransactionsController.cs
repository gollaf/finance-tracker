using FinanceTracker.Api.Common;
using FinanceTracker.Application.Transactions.AddTransaction;
using FinanceTracker.Application.Transactions.DeleteTransaction;
using FinanceTracker.Application.Transactions.UpdateTransaction;
using FinanceTracker.Domain.Common;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace FinanceTracker.Api.Transactions
{
    /// <summary>
    /// Covers Transaction's core lifecycle for now: Add, Update, Delete.
    /// Categorize and the two list/report queries (GetTransactions,
    /// GetSpendingSummary) land here in later pieces.
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
    }
}
