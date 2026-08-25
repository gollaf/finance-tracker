using FinanceTracker.Application.Categories;
using FinanceTracker.Application.Categorization;
using FinanceTracker.Application.Categorization.CreateCategorizationRule;
using FinanceTracker.Application.Common;
using FinanceTracker.Domain.Categories;
using FinanceTracker.Domain.Categorization;
using FinanceTracker.Domain.Common;
using FluentAssertions;
using NSubstitute;

namespace FinanceTracker.Application.UnitTests.Categorization.CreateCategorizationRule
{
    public class CreateCategorizationRuleCommandHandlerTests
    {
        [Fact]
        public async Task Handle_WithValidCommand_CreatesRuleAndSaves()
        {
            var category = Category.Create("Groceries");

            var categoryRepository = Substitute.For<ICategoryRepository>();
            categoryRepository.GetByIdAsync(category.Id, Arg.Any<CancellationToken>()).Returns(category);

            CategorizationRule? savedRule = null;
            var ruleRepository = Substitute.For<ICategorizationRuleRepository>();
            ruleRepository
                .AddAsync(Arg.Do<CategorizationRule>(r => savedRule = r), Arg.Any<CancellationToken>())
                .Returns(Task.CompletedTask);

            var handler = new CreateCategorizationRuleCommandHandler(categoryRepository, ruleRepository);
            var command = new CreateCategorizationRuleCommand("walmart", category.Id, 1);

            var result = await handler.Handle(command, CancellationToken.None);

            result.IsSuccess.Should().BeTrue();
            result.Value.Should().Be(savedRule!.Id);
            savedRule.Pattern.Should().Be("walmart");
            savedRule.CategoryId.Should().Be(category.Id);
            savedRule.Priority.Should().Be(1);
        }

        [Fact]
        public async Task Handle_WithUnknownCategory_ReturnsNotFound()
        {
            var categoryRepository = Substitute.For<ICategoryRepository>();
            categoryRepository
                .GetByIdAsync(Arg.Any<CategoryId>(), Arg.Any<CancellationToken>())
                .Returns((Category?)null);

            var ruleRepository = Substitute.For<ICategorizationRuleRepository>();

            var handler = new CreateCategorizationRuleCommandHandler(categoryRepository, ruleRepository);
            var command = new CreateCategorizationRuleCommand("walmart", CategoryId.New(), 1);

            var result = await handler.Handle(command, CancellationToken.None);

            result.IsFailure.Should().BeTrue();
            result.Error.Type.Should().Be(ErrorType.NotFound);
            await ruleRepository.DidNotReceive().AddAsync(Arg.Any<CategorizationRule>(), Arg.Any<CancellationToken>());
        }
    }
}
