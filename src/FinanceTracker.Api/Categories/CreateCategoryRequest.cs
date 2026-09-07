namespace FinanceTracker.Api.Categories
{
    public sealed record CreateCategoryRequest(string Name, Guid? ParentCategoryId);
}
