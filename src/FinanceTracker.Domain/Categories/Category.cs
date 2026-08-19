using FinanceTracker.Domain.Common;

namespace FinanceTracker.Domain.Categories
{
    /// <summary>
    /// A label for what a Transaction is for. Can have a parent Category for
    /// hierarchy. Name uniqueness and multi-hop cycle prevention are
    /// cross-instance concerns handled by the Application layer, not here.
    /// </summary>
    public sealed class Category
    {
        public CategoryId Id { get; }

        public string Name { get; private set; }

        public CategoryId? ParentCategoryId { get; private set; }

        private Category(CategoryId id, string name, CategoryId? parentCategoryId)
        {
            Id = id;
            Name = name;
            ParentCategoryId = parentCategoryId;
        }

        public static Category Create(string name, CategoryId? parentCategoryId = null)
        {
            ValidateName(name);
            return new Category(CategoryId.New(), name.Trim(), parentCategoryId);
        }

        public void Rename(string name)
        {
            ValidateName(name);
            Name = name.Trim();
        }

        public void Reparent(CategoryId? parentCategoryId)
        {
            if (parentCategoryId is not null && parentCategoryId.Value == Id)
                throw new ArgumentException("A category cannot be its own parent.", nameof(parentCategoryId));

            ParentCategoryId = parentCategoryId;
        }

        private static void ValidateName(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
                throw new ArgumentException("Category name is required.", nameof(name));
        }
    }
}
