using FinanceTracker.Application.Common;
using FinanceTracker.Domain.Common;
using MediatR;

namespace FinanceTracker.Application.Categories.CreateCategory
{
    /// <summary>
    /// ParentCategoryId is optional — omit it for a top-level Category. No
    /// cycle check is needed: a new Category can't be anyone's ancestor yet.
    /// </summary>
    public sealed record CreateCategoryCommand(string Name, CategoryId? ParentCategoryId = null)
        : IRequest<Result<CategoryId>>;
}
