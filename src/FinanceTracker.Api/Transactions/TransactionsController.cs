using FinanceTracker.Api.Common;
using FinanceTracker.Api.Imports;
using FinanceTracker.Application.Imports.StartImport;
using FinanceTracker.Application.Transactions.AddTransaction;
using FinanceTracker.Application.Transactions.CategorizeTransaction;
using FinanceTracker.Application.Transactions.DeleteTransaction;
using FinanceTracker.Application.Transactions.GetSpendingInsights;
using FinanceTracker.Application.Transactions.GetSpendingSummary;
using FinanceTracker.Application.Transactions.GetTransactions;
using FinanceTracker.Application.Transactions.UpdateTransaction;
using FinanceTracker.Domain.Common;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace FinanceTracker.Api.Transactions
{
    /// <summary>
    /// Covers Transaction's core lifecycle (Add, Update, Delete), assigning
    /// a Category, listing an Account's Transactions, summarizing its
    /// spending by Category for a month, generating AI spending insights,
    /// and starting a background import of a CSV file.
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

        // Same shape as spending-summary above (its own literal path
        // segment, AccountId+Year+Month from the query string), extended
        // with an AI-generated Narrative. This never fails because the AI
        // call failed -- GetSpendingInsightsQueryHandler already degrades
        // to a templated Narrative on that path and still returns
        // Result.Success; see docs/adr/0010-ai-insights-provider-and-integration-design.md.
        [HttpGet("spending-insights")]
        public async Task<IActionResult> GetSpendingInsights(
            [FromQuery] Guid accountId, [FromQuery] int year, [FromQuery] int month, CancellationToken cancellationToken)
        {
            var query = new GetSpendingInsightsQuery(new AccountId(accountId), year, month);
            var result = await _sender.Send(query, cancellationToken);

            if (result.IsFailure)
                return result.ToActionResult();

            var response = new SpendingInsightsResponse(
                result.Value.Trends
                    .Select(t => new CategoryTrendResponse(
                        t.CategoryId?.Value,
                        t.CategoryName,
                        t.CurrentMonthTotal.Amount,
                        t.PriorAverageTotal.Amount,
                        t.CurrentMonthTotal.Currency,
                        t.PercentChange))
                    .ToList(),
                result.Value.Narrative,
                result.Value.NarrativeGeneratedByAi);

            return Ok(response);
        }

        // The file arrives as multipart/form-data (see ImportTransactionsRequest's
        // own doc comment for why that one DTO isn't a record). Parsing the raw
        // CSV text into structured rows is this layer's job (ADR 0006); the
        // import itself runs later in the Worker (ADR 0016). A row that fails
        // to parse is recorded on the import job the same way a row that
        // fails a domain rule later is: as an entry in its Errors, never as a
        // request-level failure.
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

            var (rows, parseErrors) = CsvTransactionRowParser.Parse(csvContent);

            // Not a single row parsed (or the file had no data rows at
            // all): there's nothing to import, so no job is created and the
            // uploader gets the parse errors immediately, in the response,
            // instead of being sent off to poll a job with nothing in it.
            if (rows.Count == 0)
            {
                var problem = new ProblemDetails
                {
                    Status = StatusCodes.Status400BadRequest,
                    Title = "Import.NoValidRows",
                    Detail = "None of the file's rows could be parsed, so nothing was imported.",
                };
                problem.Extensions["errors"] = parseErrors
                    .Select(e => new ImportRowErrorResponse(e.RowNumber, e.Message))
                    .ToList();

                return new ObjectResult(problem) { StatusCode = StatusCodes.Status400BadRequest };
            }

            var command = new StartImportCommand(new AccountId(request.AccountId), rows, parseErrors);
            var result = await _sender.Send(command, cancellationToken);

            if (result.IsFailure)
                return result.ToActionResult();

            // 202 Accepted: "your request is valid and has been taken on,
            // but the work isn't done yet". The Location header tells the
            // client where to poll for the outcome (ADR 0016).
            var importJobId = result.Value.Value;
            return Accepted($"/api/imports/{importJobId}", new StartImportResponse(importJobId));
        }
    }
}
