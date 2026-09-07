using FinanceTracker.Api.Common;
using FinanceTracker.Application.Accounts.CreateAccount;
using FinanceTracker.Application.Accounts.GetAccountBalance;
using FinanceTracker.Domain.Common;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace FinanceTracker.Api.Accounts
{
    /// <summary>
    /// Each action is thin on purpose, per ADR 0004: build the MediatR
    /// request from the incoming DTO, Send it, and either map a failure
    /// through ResultExtensions.ToActionResult() or shape the success
    /// response -- no business logic here.
    /// </summary>
    [ApiController]
    [Route("api/accounts")]
    public sealed class AccountsController : ControllerBase
    {
        private readonly ISender _sender;

        public AccountsController(ISender sender)
        {
            _sender = sender;
        }

        [HttpPost]
        public async Task<IActionResult> Create(CreateAccountRequest request, CancellationToken cancellationToken)
        {
            var command = new CreateAccountCommand(request.Name, request.Type, request.Currency);
            var result = await _sender.Send(command, cancellationToken);

            if (result.IsFailure)
                return result.ToActionResult();

            var accountId = result.Value.Value;
            var response = new CreateAccountResponse(accountId);

            // No GetById action exists yet to point CreatedAtAction at, so
            // the Location header is a plain, honest URL -- not a route
            // name -- pointing at where the resource will be once one does.
            return Created($"/api/accounts/{accountId}", response);
        }

        [HttpGet("{id:guid}/balance")]
        public async Task<IActionResult> GetBalance(Guid id, CancellationToken cancellationToken)
        {
            var query = new GetAccountBalanceQuery(new AccountId(id));
            var result = await _sender.Send(query, cancellationToken);

            if (result.IsFailure)
                return result.ToActionResult();

            var response = new AccountBalanceResponse(result.Value.Amount, result.Value.Currency);
            return Ok(response);
        }
    }
}
