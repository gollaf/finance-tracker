using FinanceTracker.Api.Common;
using FinanceTracker.Api.Transactions;
using FinanceTracker.Application.Imports.GetImportJob;
using FinanceTracker.Domain.Common;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace FinanceTracker.Api.Imports
{
    /// <summary>
    /// Read-only view of import jobs, which are started through
    /// POST /api/transactions/import. See docs/adr/0016-asynchronous-csv-import.md.
    /// </summary>
    [ApiController]
    [Route("api/imports")]
    public sealed class ImportsController : ControllerBase
    {
        private readonly ISender _sender;

        public ImportsController(ISender sender)
        {
            _sender = sender;
        }

        [HttpGet("{id:guid}")]
        public async Task<IActionResult> GetById(Guid id, CancellationToken cancellationToken)
        {
            var result = await _sender.Send(new GetImportJobQuery(new ImportJobId(id)), cancellationToken);

            if (result.IsFailure)
                return result.ToActionResult();

            var job = result.Value;

            return Ok(new ImportJobResponse(
                job.Id.Value,
                job.AccountId.Value,
                job.Status,
                job.TotalRows,
                job.ImportedCount,
                job.Errors.Select(e => new ImportRowErrorResponse(e.RowNumber, e.Message)).ToList(),
                job.FailureReason,
                job.CreatedAt,
                job.CompletedAt));
        }
    }
}
