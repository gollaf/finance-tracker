using FinanceTracker.Application.Accounts;
using FinanceTracker.Application.Categorization;
using FinanceTracker.Application.Common.IntegrationEvents;
using FinanceTracker.Application.Imports;
using FinanceTracker.Application.Imports.ProcessImport;
using FinanceTracker.Application.Transactions;
using FinanceTracker.Application.Transactions.IntegrationEvents;
using FinanceTracker.Application.UnitTests.TestDoubles;
using FinanceTracker.Domain.Accounts;
using FinanceTracker.Domain.Categorization;
using FinanceTracker.Domain.Common;
using FinanceTracker.Domain.Imports;
using FinanceTracker.Domain.Transactions;
using FluentAssertions;
using NSubstitute;

namespace FinanceTracker.Application.UnitTests.Imports.ProcessImport
{
    public class ProcessImportJobCommandHandlerTests
    {
        private static readonly DateOnly Today = DateOnly.FromDateTime(DateTime.UtcNow);

        private readonly InlineUnitOfWork _unitOfWork = new();
        private readonly IImportJobRepository _importJobRepository = Substitute.For<IImportJobRepository>();
        private readonly IAccountRepository _accountRepository = Substitute.For<IAccountRepository>();
        private readonly ITransactionRepository _transactionRepository = Substitute.For<ITransactionRepository>();
        private readonly ICategorizationRuleRepository _ruleRepository = Substitute.For<ICategorizationRuleRepository>();
        private readonly IOutbox _outbox = Substitute.For<IOutbox>();

        private readonly Account _account = Account.Create("Checking", AccountType.Checking, "USD");

        public ProcessImportJobCommandHandlerTests()
        {
            _accountRepository.GetByIdAsync(_account.Id, Arg.Any<CancellationToken>()).Returns(_account);
            _ruleRepository.GetAllAsync(Arg.Any<CancellationToken>()).Returns(Array.Empty<CategorizationRule>());
        }

        private ProcessImportJobCommandHandler NewHandler() =>
            new(_unitOfWork, _importJobRepository, _accountRepository, _transactionRepository, _ruleRepository, _outbox);

        private static ImportJobRow Row(int rowNumber, string description, TransactionType type = TransactionType.Expense) =>
            new(rowNumber, 10m, type, description, Today);

        private ImportJob GivenJob(ImportJobRow[] rows, params ImportJobRowError[] parseErrors)
        {
            var job = ImportJob.Create(_account.Id, rows, parseErrors);
            _importJobRepository.GetByIdAsync(job.Id, Arg.Any<CancellationToken>()).Returns(job);
            return job;
        }

        private async Task<ProcessImportJobOutcome> HandleAsync(ImportJobId importJobId)
        {
            var result = await NewHandler().Handle(new ProcessImportJobCommand(importJobId), CancellationToken.None);

            result.IsSuccess.Should().BeTrue();
            return result.Value;
        }

        [Fact]
        public async Task Handle_ImportsValidRows_RecordsFailedOnes_AndCompletesTheJob()
        {
            var job = GivenJob(
                new[] { Row(2, "Coffee"), Row(3, "   "), Row(4, "Salary", TransactionType.Income) },
                new ImportJobRowError(5, "Invalid amount."));

            var outcome = await HandleAsync(job.Id);

            outcome.Should().Be(ProcessImportJobOutcome.Completed);
            job.Status.Should().Be(ImportJobStatus.Completed);
            job.ImportedCount.Should().Be(2);
            // Row 3's blank description fails Transaction.Create; row 5 failed
            // parsing at upload. Both end up in one list, in line order.
            job.Errors.Select(e => e.RowNumber).Should().Equal(3, 5);

            await _transactionRepository.Received(2).AddAsync(Arg.Any<Transaction>(), Arg.Any<CancellationToken>());
            _outbox.Received(2).Enqueue(Arg.Any<TransactionAdded>());
            await _importJobRepository.Received(1).UpdateAsync(job, Arg.Any<CancellationToken>());
        }

        [Fact]
        public async Task Handle_RunsEverythingInsideOneUnitOfWork()
        {
            var job = GivenJob(new[] { Row(2, "Coffee") });

            await HandleAsync(job.Id);

            _unitOfWork.Calls.Should().Be(1);
        }

        [Fact]
        public async Task Handle_EnqueuesEachTransactionsEventBeforeSavingIt()
        {
            var job = GivenJob(new[] { Row(2, "Coffee"), Row(3, "Tea") });

            await HandleAsync(job.Id);

            Received.InOrder(() =>
            {
                _outbox.Enqueue(Arg.Any<TransactionAdded>());
                _ = _transactionRepository.AddAsync(Arg.Any<Transaction>(), Arg.Any<CancellationToken>());
                _outbox.Enqueue(Arg.Any<TransactionAdded>());
                _ = _transactionRepository.AddAsync(Arg.Any<Transaction>(), Arg.Any<CancellationToken>());
                _ = _importJobRepository.UpdateAsync(job, Arg.Any<CancellationToken>());
            });
        }

        [Fact]
        public async Task Handle_AppliesCategorizationRules()
        {
            var categoryId = CategoryId.New();
            _ruleRepository.GetAllAsync(Arg.Any<CancellationToken>())
                .Returns(new[] { CategorizationRule.Create("coffee", categoryId, priority: 1) });

            Transaction? saved = null;
            _transactionRepository.AddAsync(Arg.Do<Transaction>(t => saved = t), Arg.Any<CancellationToken>());

            var job = GivenJob(new[] { Row(2, "Coffee Shop") });

            await HandleAsync(job.Id);

            saved!.CategoryId.Should().Be(categoryId);
        }

        [Fact]
        public async Task Handle_WhenJobWasAlreadyProcessed_DoesNothing()
        {
            var job = GivenJob(new[] { Row(2, "Coffee") });
            job.Complete(1, Array.Empty<ImportJobRowError>());

            var outcome = await HandleAsync(job.Id);

            outcome.Should().Be(ProcessImportJobOutcome.AlreadyProcessed);
            await _transactionRepository.DidNotReceive().AddAsync(Arg.Any<Transaction>(), Arg.Any<CancellationToken>());
            await _importJobRepository.DidNotReceive().UpdateAsync(Arg.Any<ImportJob>(), Arg.Any<CancellationToken>());
        }

        [Fact]
        public async Task Handle_WhenJobDoesNotExist_ReportsJobNotFound()
        {
            _importJobRepository.GetByIdAsync(Arg.Any<ImportJobId>(), Arg.Any<CancellationToken>()).Returns((ImportJob?)null);

            var outcome = await HandleAsync(ImportJobId.New());

            outcome.Should().Be(ProcessImportJobOutcome.JobNotFound);
        }

        [Fact]
        public async Task Handle_WhenAccountWasClosedMeanwhile_FailsTheJobWithoutImporting()
        {
            var job = GivenJob(new[] { Row(2, "Coffee") });
            _account.Close();

            var outcome = await HandleAsync(job.Id);

            outcome.Should().Be(ProcessImportJobOutcome.Failed);
            job.Status.Should().Be(ImportJobStatus.Failed);
            job.FailureReason.Should().NotBeNullOrWhiteSpace();
            await _transactionRepository.DidNotReceive().AddAsync(Arg.Any<Transaction>(), Arg.Any<CancellationToken>());
            await _importJobRepository.Received(1).UpdateAsync(job, Arg.Any<CancellationToken>());
        }

        [Fact]
        public async Task Handle_WhenAccountNoLongerExists_FailsTheJob()
        {
            var job = GivenJob(new[] { Row(2, "Coffee") });
            _accountRepository.GetByIdAsync(_account.Id, Arg.Any<CancellationToken>()).Returns((Account?)null);

            var outcome = await HandleAsync(job.Id);

            outcome.Should().Be(ProcessImportJobOutcome.Failed);
            job.Status.Should().Be(ImportJobStatus.Failed);
        }

        [Fact]
        public async Task Handle_WhenSavingFailsUnexpectedly_LetsTheExceptionEscapeWithoutCompletingTheJob()
        {
            var job = GivenJob(new[] { Row(2, "Coffee") });
            _transactionRepository
                .AddAsync(Arg.Any<Transaction>(), Arg.Any<CancellationToken>())
                .Returns(Task.FromException(new InvalidOperationException("Database went away.")));

            var act = () => NewHandler().Handle(new ProcessImportJobCommand(job.Id), CancellationToken.None);

            // Not caught as a row error: only ArgumentException is. Escaping
            // is what makes the real unit of work roll back, and the message
            // be retried from a clean state.
            await act.Should().ThrowAsync<InvalidOperationException>();
            await _importJobRepository.DidNotReceive().UpdateAsync(Arg.Any<ImportJob>(), Arg.Any<CancellationToken>());
        }
    }
}
