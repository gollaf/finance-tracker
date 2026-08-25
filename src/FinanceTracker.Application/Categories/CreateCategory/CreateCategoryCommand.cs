using FinanceTracker.Application.Common;
using FinanceTracker.Domain.Common;
using MediatR;

namespace FinanceTracker.Application.Categories.CreateCategory
{
    /// <summary>
    /// ParentCategoryId is optional — omit it for a top-level Category.
    /// Cycle prevention isn't a concern here: a brand-new Category can't
    /// already be an ancestor of anything, so it can only matter once a
    /// Category is later reparented, not at creation.
    /// </summary>
    public sealed record CreateCategoryCommand(string Name, CategoryId? ParentCategoryId = null)
        : IRequest<Result<CategoryId>>;
}
