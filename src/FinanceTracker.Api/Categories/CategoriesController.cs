using FinanceTracker.Api.Common;
using FinanceTracker.Application.Categories.CreateCategory;
using FinanceTracker.Domain.Common;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace FinanceTracker.Api.Categories
{
    [ApiController]
    [Route("api/categories")]
    public sealed class CategoriesController : ControllerBase
    {
        private readonly ISender _sender;

        public CategoriesController(ISender sender)
        {
            _sender = sender;
        }

        [HttpPost]
        public async Task<IActionResult> Create(CreateCategoryRequest request, CancellationToken cancellationToken)
        {
            var parentCategoryId = request.ParentCategoryId is { } id ? new CategoryId(id) : (CategoryId?)null;
            var command = new CreateCategoryCommand(request.Name, parentCategoryId);
            var result = await _sender.Send(command, cancellationToken);

            if (result.IsFailure)
                return result.ToActionResult();

            var categoryId = result.Value.Value;
            var response = new CreateCategoryResponse(categoryId);

            return Created($"/api/categories/{categoryId}", response);
        }
    }
}
