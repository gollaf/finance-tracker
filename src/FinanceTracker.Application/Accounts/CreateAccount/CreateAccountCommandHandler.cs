using FinanceTracker.Application.Common;
using FinanceTracker.Domain.Accounts;
using FinanceTracker.Domain.Common;
using MediatR;

namespace FinanceTracker.Application.Accounts.CreateAccount
{
    /// <summary>
    /// Runs after ValidationBehavior has already confirmed the command is
    /// well-formed, so Account.Create is trusted not to throw here. If it
    /// ever does, the validator and the Domain invariant it mirrors have
    /// drifted apart — that should fail loudly, not be swallowed into an
    /// ordinary Result failure.
    /// </summary>
    public sealed class CreateAccountCommandHandler : IRequestHandler<CreateAccountCommand, Result<AccountId>>
    {
        private readonly IAccountRepository _accountRepository;

        public CreateAccountCommandHandler(IAccountRepository accountRepository)
        {
            _accountRepository = accountRepository;
        }

        public async Task<Result<AccountId>> Handle(CreateAccountCommand request, CancellationToken cancellationToken)
        {
            var account = Account.Create(request.Name, request.Type, request.Currency);

            await _accountRepository.AddAsync(account, cancellationToken);

            return account.Id;
        }
    }
}
