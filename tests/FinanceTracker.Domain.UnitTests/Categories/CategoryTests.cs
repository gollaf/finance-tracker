using FinanceTracker.Domain.Categories;
using FinanceTracker.Domain.Common;
using FluentAssertions;

namespace FinanceTracker.Domain.UnitTests.Categories
{
    public class CategoryTests
    {
        [Fact]
        public void Create_WithValidName_Succeeds()
        {
            var category = Category.Create("Groceries");

            category.Name.Should().Be("Groceries");
            category.ParentCategoryId.Should().BeNull();
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        public void Create_WithMissingName_Throws(string? name)
        {
            var act = () => Category.Create(name!);

            act.Should().Throw<ArgumentException>();
        }

        [Fact]
        public void Create_WithParent_SetsParentCategoryId()
        {
            var parentId = CategoryId.New();

            var category = Category.Create("Dining Out", parentId);

            category.ParentCategoryId.Should().Be(parentId);
        }

        [Fact]
        public void Rename_WithValidName_UpdatesName()
        {
            var category = Category.Create("Groceries");

            category.Rename("Food");

            category.Name.Should().Be("Food");
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        public void Rename_WithMissingName_Throws(string? name)
        {
            var category = Category.Create("Groceries");

            var act = () => category.Rename(name!);

            act.Should().Throw<ArgumentException>();
        }

        [Fact]
        public void Reparent_ToItself_Throws()
        {
            var category = Category.Create("Groceries");

            var act = () => category.Reparent(category.Id);

            act.Should().Throw<ArgumentException>();
        }

        [Fact]
        public void Reparent_ToAnotherCategory_UpdatesParent()
        {
            var category = Category.Create("Dining Out");
            var newParentId = CategoryId.New();

            category.Reparent(newParentId);

            category.ParentCategoryId.Should().Be(newParentId);
        }

        [Fact]
        public void Reparent_ToNull_ClearsParent()
        {
            var category = Category.Create("Dining Out", CategoryId.New());

            category.Reparent(null);

            category.ParentCategoryId.Should().BeNull();
        }
    }
}
