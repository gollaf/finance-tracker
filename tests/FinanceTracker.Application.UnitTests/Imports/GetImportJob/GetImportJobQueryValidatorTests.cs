using FinanceTracker.Application.Imports.GetImportJob;
using FinanceTracker.Domain.Common;
using FluentAssertions;

namespace FinanceTracker.Application.UnitTests.Imports.GetImportJob
{
    public class GetImportJobQueryValidatorTests
    {
        private readonly GetImportJobQueryValidator _validator = new();

        [Fact]
        public void Validate_WithImportJobId_IsValid()
        {
            _validator.Validate(new GetImportJobQuery(ImportJobId.New())).IsValid.Should().BeTrue();
        }

        [Fact]
        public void Validate_WithDefaultImportJobId_IsInvalid()
        {
            _validator.Validate(new GetImportJobQuery(default)).IsValid.Should().BeFalse();
        }
    }
}
