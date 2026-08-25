using FinanceTracker.Application.Categories;
using FinanceTracker.Application.Categories.CreateCategory;
using FinanceTracker.Application.Common;
using FinanceTracker.Domain.Categories;
using FinanceTracker.Domain.Common;
using FluentAssertions;
using NSubstitute;

namespace FinanceTracker.Application.UnitTests.Categories.CreateCategory
{
    public class CreateCategoryCommandHandlerTests
    {
        [Fact]
        public async Task Handle_WithUniqueNameAndNoParent_CreatesCategoryAndSaves()
        {
            var categoryRepository = Substitute.For<ICategoryRepository>();
            categoryRepository.ExistsWithNameAsync("Groceries", Arg.Any<CancellationToken>()).Returns(false);

            Category? savedCategory = null;
            categoryRepository
                .AddAsync(Arg.Do<Category>(c => savedCategory = c), Arg.Any<CancellationToken>())
                .Returns(Task.CompletedTask);

            var handler = new CreateCategoryCommandHandler(categoryRepository);
            var command = new CreateCategoryCommand("Groceries");

            var result = await handler.Handle(command, CancellationToken.None);

            result.IsSuccess.Should().BeTrue();
            result.Value.Should().Be(savedCategory!.Id);
            savedCategory.Name.Should().Be("Groceries");
            savedCategory.ParentCategoryId.Should().BeNull();
        }

        [Fact]
        public async Task Handle_WithDuplicateName_ReturnsConflict()
        {
            var categoryRepository = Substitute.For<ICategoryRepository>();
            categoryRepository.ExistsWithNameAsync("Groceries", Arg.Any<CancellationToken>()).Returns(true);

            var handler = new CreateCategoryCommandHandler(categoryRepository);
            var command = new CreateCategoryCommand("Groceries");

            var result = await handler.Handle(command, CancellationToken.None);

            result.IsFailure.Should().BeTrue();
            result.Error.Type.Should().Be(ErrorType.Conflict);
            await categoryRepository.DidNotReceive().AddAsync(Arg.Any<Category>(), Arg.Any<CancellationToken>());
        }

        [Fact]
        public async Task Handle_WithValidParent_CreatesChildCategory()
        {
            var parent = Category.Create("Food");

            var categoryRepository = Substitute.For<ICategoryRepository>();
            categoryRepository.ExistsWithNameAsync("Groceries", Arg.Any<CancellationToken>()).Returns(false);
            categoryRepository.GetByIdAsync(parent.Id, Arg.Any<CancellationToken>()).Returns(parent);

            Category? savedCategory = null;
            categoryRepository
                .AddAsync(Arg.Do<Category>(c => savedCategory = c), Arg.Any<CancellationToken>())
                .Returns(Task.CompletedTask);

            var handler = new CreateCategoryCommandHandler(categoryRepository);
            var command = new CreateCategoryCommand("Groceries", parent.Id);

            var result = await handler.Handle(command, CancellationToken.None);

            result.IsSuccess.Should().BeTrue();
            savedCategory!.ParentCategoryId.Should().Be(parent.Id);
        }

        [Fact]
        public async Task Handle_WithUnknownParent_ReturnsNotFound()
        {
            var categoryRepository = Substitute.For<ICategoryRepository>();
            categoryRepository.ExistsWithNameAsync("Groceries", Arg.Any<CancellationToken>()).Returns(false);
            categoryRepository
                .GetByIdAsync(Arg.Any<CategoryId>(), Arg.Any<CancellationToken>())
                .Returns((Category?)null);

            var handler = new CreateCategoryCommandHandler(categoryRepository);
            var command = new CreateCategoryCommand("Groceries", CategoryId.New());

            var result = await handler.Handle(command, CancellationToken.None);

            result.IsFailure.Should().BeTrue();
            result.Error.Type.Should().Be(ErrorType.NotFound);
            await categoryRepository.DidNotReceive().AddAsync(Arg.Any<Category>(), Arg.Any<CancellationToken>());
        }
    }
}
