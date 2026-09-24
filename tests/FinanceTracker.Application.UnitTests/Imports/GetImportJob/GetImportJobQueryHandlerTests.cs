using FinanceTracker.Application.Common;
using FinanceTracker.Application.Imports;
using FinanceTracker.Application.Imports.GetImportJob;
using FinanceTracker.Domain.Common;
using FinanceTracker.Domain.Imports;
using FinanceTracker.Domain.Transactions;
using FluentAssertions;
using NSubstitute;

namespace FinanceTracker.Application.UnitTests.Imports.GetImportJob
{
    public class GetImportJobQueryHandlerTests
    {
        private static readonly DateOnly Today = DateOnly.FromDateTime(DateTime.UtcNow);

        private readonly IImportJobRepository _importJobRepository = Substitute.For<IImportJobRepository>();

        [Fact]
        public async Task Handle_WithExistingJob_ReturnsItsOutcomeWithoutTheRawRows()
        {
            var job = ImportJob.Create(
                AccountId.New(),
                new[]
                {
                    new ImportJobRow(2, 4.5m, TransactionType.Expense, "Coffee", Today),
                    new ImportJobRow(3, 9m, TransactionType.Expense, "Lunch", Today),
                },
                new[] { new ImportJobRowError(4, "Invalid amount.") });
            job.Complete(2, Array.Empty<ImportJobRowError>());
            _importJobRepository.GetByIdAsync(job.Id, Arg.Any<CancellationToken>()).Returns(job);

            var result = await new GetImportJobQueryHandler(_importJobRepository)
                .Handle(new GetImportJobQuery(job.Id), CancellationToken.None);

            result.IsSuccess.Should().BeTrue();
            result.Value.Id.Should().Be(job.Id);
            result.Value.Status.Should().Be(ImportJobStatus.Completed);
            result.Value.TotalRows.Should().Be(2);
            result.Value.ImportedCount.Should().Be(2);
            result.Value.Errors.Should().ContainSingle().Which.RowNumber.Should().Be(4);
        }

        [Fact]
        public async Task Handle_WithUnknownJob_ReturnsNotFound()
        {
            _importJobRepository.GetByIdAsync(Arg.Any<ImportJobId>(), Arg.Any<CancellationToken>()).Returns((ImportJob?)null);

            var result = await new GetImportJobQueryHandler(_importJobRepository)
                .Handle(new GetImportJobQuery(ImportJobId.New()), CancellationToken.None);

            result.IsFailure.Should().BeTrue();
            result.Error.Type.Should().Be(ErrorType.NotFound);
        }
    }
}
