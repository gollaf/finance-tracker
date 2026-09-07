using FinanceTracker.Api.Common;
using FinanceTracker.Application.Transactions.AddTransaction;
using FinanceTracker.Application.Transactions.CategorizeTransaction;
using FinanceTracker.Application.Transactions.DeleteTransaction;
using FinanceTracker.Application.Transactions.GetSpendingSummary;
using FinanceTracker.Application.Transactions.GetTransactions;
using FinanceTracker.Application.Transactions.ImportTransactionsFromCsv;
using FinanceTracker.Application.Transactions.UpdateTransaction;
using FinanceTracker.Domain.Common;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace FinanceTracker.Api.Transactions
{
    /// <summary>
    /// Covers Transaction's core lifecycle (Add, Update, Delete), assigning
    /// a Category, listing an Account's Transactions, summarizing its
    /// spending by Category for a month, and importing a batch from CSV.
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

        // spending-summary is its own literal path segment, not a route
        // parameter -- it reports across every Transaction matching
        // AccountId+Year+Month, so there's no single Transaction id to
        // hang this off of the way Categorize hangs off {id}.
        [HttpGet("spending-summary")]
        public async Task<IActionResult> GetSpendingSummary(
            [FromQuery] Guid accountId, [FromQuery] int year, [FromQuery] int month, CancellationToken cancellationToken)
        {
            var query = new GetSpendingSummaryQuery(new AccountId(accountId), year, month);
            var result = await _sender.Send(query, cancellationToken);

            if (result.IsFailure)
                return result.ToActionResult();

            var response = result.Value
                .Select(s => new CategorySpendingResponse(s.CategoryId?.Value, s.Total.Amount, s.Total.Currency))
                .ToList();

            return Ok(response);
        }

        // The file arrives as multipart/form-data (see ImportTransactionsRequest's
        // own doc comment for why that one DTO isn't a record). Parsing the raw
        // CSV text into structured rows is this layer's job -- CsvTransactionRow's
        // doc comment in Application says as much -- and a row that fails even
        // that parse is reported the same way a row that parses but fails a
        // domain rule is: as an entry in Errors, never as a request-level failure.
        [HttpPost("import")]
        public async Task<IActionResult> Import([FromForm] ImportTransactionsRequest request, CancellationToken cancellationToken)
        {
            if (request.File is null || request.File.Length == 0)
            {
                return Problem(
                    statusCode: StatusCodes.Status400BadRequest,
                    title: "Transaction.EmptyFile",
                    detail: "A non-empty CSV file is required.");
            }

            string csvContent;
            using (var reader = new StreamReader(request.File.OpenReadStream()))
            {
                csvContent = await reader.ReadToEndAsync(cancellationToken);
            }

            var (parsedRows, parseErrors) = CsvTransactionRowParser.Parse(csvContent);

            // Every row failed to parse (or the file had no data rows at all) --
            // there's nothing valid to hand the command, and sending it an empty
            // Rows list would just turn into a generic 400 from its own validator,
            // burying the real per-row parse errors. Report them directly instead.
            if (parsedRows.Count == 0)
            {
                return Ok(new ImportTransactionsResponse(Array.Empty<Guid>(), parseErrors));
            }

            var command = new ImportTransactionsFromCsvCommand(
                new AccountId(request.AccountId), parsedRows.Select(r => r.Row).ToList());
            var result = await _sender.Send(command, cancellationToken);

            if (result.IsFailure)
                return result.ToActionResult();

            // The command's own Errors carry a RowIndex into the filtered list of
            // rows it was actually given, not the file's line numbers -- translate
            // each one back through ParsedCsvRow.OriginalIndex before merging with
            // this layer's own parseErrors, which already speak in file line numbers.
            var handlerErrors = result.Value.Errors
                .Select(e => new ImportRowErrorResponse(parsedRows[e.RowIndex].OriginalIndex + 2, e.Message));

            var allErrors = parseErrors.Concat(handlerErrors).OrderBy(e => e.RowNumber).ToList();

            var response = new ImportTransactionsResponse(
                result.Value.ImportedTransactionIds.Select(id => id.Value).ToList(), allErrors);

            return Ok(response);
        }
    }
}
