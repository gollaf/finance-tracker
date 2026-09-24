using FinanceTracker.Application.Accounts;
using FinanceTracker.Application.Common;
using FinanceTracker.Application.Common.IntegrationEvents;
using FinanceTracker.Application.Imports;
using FinanceTracker.Application.Imports.IntegrationEvents;
using FinanceTracker.Application.Imports.StartImport;
using FinanceTracker.Domain.Accounts;
using FinanceTracker.Domain.Common;
using FinanceTracker.Domain.Imports;
using FinanceTracker.Domain.Transactions;
using FluentAssertions;
using NSubstitute;

namespace FinanceTracker.Application.UnitTests.Imports.StartImport
{
    public class StartImportCommandHandlerTests
    {
        private static readonly DateOnly Today = DateOnly.FromDateTime(DateTime.UtcNow);

        private readonly IAccountRepository _accountRepository = Substitute.For<IAccountRepository>();
        private readonly IImportJobRepository _importJobRepository = Substitute.For<IImportJobRepository>();
        private readonly IOutbox _outbox = Substitute.For<IOutbox>();

        private StartImportCommandHandler NewHandler() => new(_accountRepository, _importJobRepository, _outbox);

        private static StartImportCommand NewCommand(AccountId accountId) =>
            new(
                accountId,
                new[]
                {
                    new ImportJobRow(2, 4.5m, TransactionType.Expense, "Coffee", Today),
                    new ImportJobRow(4, 1200m, TransactionType.Income, "Salary", Today),
                },
                new[] { new ImportJobRowError(3, "Invalid amount.") });

        [Fact]
        public async Task Handle_WithOpenAccount_CreatesAPendingJobAndRequestsItsProcessing()
        {
            var account = Account.Create("Checking", AccountType.Checking, "USD");
            _accountRepository.GetByIdAsync(account.Id, Arg.Any<CancellationToken>()).Returns(account);

            var result = await NewHandler().Handle(NewCommand(account.Id), CancellationToken.None);

            result.IsSuccess.Should().BeTrue();
            var jobId = result.Value;

            await _importJobRepository.Received(1).AddAsync(
                Arg.Is<ImportJob>(j =>
                    j.Id == jobId
                    && j.AccountId == account.Id
                    && j.Status == ImportJobStatus.Pending
                    && j.Rows.Count == 2
                    && j.Errors.Count == 1),
                Arg.Any<CancellationToken>());

            // Staged before the save that writes it (IOutbox's ordering rule).
            Received.InOrder(() =>
            {
                _outbox.Enqueue(Arg.Is<ImportRequested>(e => e.ImportJobId == jobId.Value));
                _ = _importJobRepository.AddAsync(Arg.Any<ImportJob>(), Arg.Any<CancellationToken>());
            });
        }

        [Fact]
        public async Task Handle_WithUnknownAccount_ReturnsNotFoundAndCreatesNothing()
        {
            _accountRepository.GetByIdAsync(Arg.Any<AccountId>(), Arg.Any<CancellationToken>()).Returns((Account?)null);

            var result = await NewHandler().Handle(NewCommand(AccountId.New()), CancellationToken.None);

            result.IsFailure.Should().BeTrue();
            result.Error.Type.Should().Be(ErrorType.NotFound);
            await _importJobRepository.DidNotReceive().AddAsync(Arg.Any<ImportJob>(), Arg.Any<CancellationToken>());
            _outbox.DidNotReceive().Enqueue(Arg.Any<ImportRequested>());
        }

        [Fact]
        public async Task Handle_WithClosedAccount_ReturnsConflictAndCreatesNothing()
        {
            var account = Account.Create("Checking", AccountType.Checking, "USD");
            account.Close();
            _accountRepository.GetByIdAsync(account.Id, Arg.Any<CancellationToken>()).Returns(account);

            var result = await NewHandler().Handle(NewCommand(account.Id), CancellationToken.None);

            result.IsFailure.Should().BeTrue();
            result.Error.Type.Should().Be(ErrorType.Conflict);
            await _importJobRepository.DidNotReceive().AddAsync(Arg.Any<ImportJob>(), Arg.Any<CancellationToken>());
        }
    }
}
