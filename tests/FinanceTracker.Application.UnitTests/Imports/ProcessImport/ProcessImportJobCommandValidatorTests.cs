using FinanceTracker.Application.Imports.ProcessImport;
using FinanceTracker.Domain.Common;
using FluentAssertions;

namespace FinanceTracker.Application.UnitTests.Imports.ProcessImport
{
    public class ProcessImportJobCommandValidatorTests
    {
        private readonly ProcessImportJobCommandValidator _validator = new();

        [Fact]
        public void Validate_WithImportJobId_IsValid()
        {
            _validator.Validate(new ProcessImportJobCommand(ImportJobId.New())).IsValid.Should().BeTrue();
        }

        [Fact]
        public void Validate_WithDefaultImportJobId_IsInvalid()
        {
            _validator.Validate(new ProcessImportJobCommand(default)).IsValid.Should().BeFalse();
        }
    }
}
