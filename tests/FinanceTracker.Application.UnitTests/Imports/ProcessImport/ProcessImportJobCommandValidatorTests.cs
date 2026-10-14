using FinanceTracker.Application.Imports.ProcessImport;
using FinanceTracker.Domain.Common;
using FluentAssertions;

namespace FinanceTracker.Application.UnitTests.Imports.ProcessImport
{
    public class ProcessImportJobCommandValidatorTests
    {
        private static readonly UserId Owner = new(Guid.NewGuid());
        private readonly ProcessImportJobCommandValidator _validator = new();

        [Fact]
        public void Validate_WithImportJobId_IsValid()
        {
            _validator.Validate(new ProcessImportJobCommand(Owner, ImportJobId.New())).IsValid.Should().BeTrue();
        }

        [Fact]
        public void Validate_WithDefaultImportJobId_IsInvalid()
        {
            _validator.Validate(new ProcessImportJobCommand(Owner, default)).IsValid.Should().BeFalse();
        }

        [Fact]
        public void Validate_WithDefaultOwnerId_IsInvalid()
        {
            var result = _validator.Validate(new ProcessImportJobCommand(Owner, ImportJobId.New()) with { OwnerId = default });

            result.IsValid.Should().BeFalse();
            result.Errors.Should().Contain(e => e.PropertyName == nameof(ProcessImportJobCommand.OwnerId));
        }
    }
}
