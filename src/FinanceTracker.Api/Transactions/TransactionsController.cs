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

        [HttpDelete("{id:guid}")]
        public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
        {
            var command = new DeleteTransactionCommand(new TransactionId(id));
            var result = await _sender.Send(command, cancellationToken);

            if (result.IsFailure)
                return result.ToActionResult();

            return NoContent();
        }

        // PUT on a "category" sub-resource; a null CategoryId clears it.
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

        // AccountId is a filter, not a parent resource: Transaction is its
        // own aggregate, so this isn't nested under /api/accounts.
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

        // Never fails because of the AI: the handler falls back to a
        // templated Narrative.
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

        // Parses the CSV here; the import itself runs later in the Worker
        // (ADR 0016). Unparseable rows are recorded on the job's Errors.
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

            // Nothing to import: return the parse errors now instead of a
            // job with nothing in it.
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

            // 202 Accepted, with the job to poll in the Location header.
            var importJobId = result.Value.Value;
            return Accepted($"/api/imports/{importJobId}", new StartImportResponse(importJobId));
        }
    }
}
