using FinanceTracker.Application.Common;
using FinanceTracker.Domain.Categories;
using FinanceTracker.Domain.Common;
using MediatR;

namespace FinanceTracker.Application.Categories.CreateCategory
{
    public sealed class CreateCategoryCommandHandler : IRequestHandler<CreateCategoryCommand, Result<CategoryId>>
    {
        private readonly ICategoryRepository _categoryRepository;

        public CreateCategoryCommandHandler(ICategoryRepository categoryRepository)
        {
            _categoryRepository = categoryRepository;
        }

        public async Task<Result<CategoryId>> Handle(CreateCategoryCommand request, CancellationToken cancellationToken)
        {
            var nameExists = await _categoryRepository.ExistsWithNameAsync(request.Name, cancellationToken);

            if (nameExists)
            {
                return Result.Failure<CategoryId>(Error.Conflict(
                    "Category.DuplicateName", $"A category named '{request.Name}' already exists."));
            }

            if (request.ParentCategoryId is { } parentCategoryId)
            {
                var parent = await _categoryRepository.GetByIdAsync(parentCategoryId, cancellationToken);

                if (parent is null)
                {
                    return Result.Failure<CategoryId>(Error.NotFound(
                        "Category.NotFound", $"No category found with id '{parentCategoryId}'."));
                }
            }

            var category = Category.Create(request.Name, request.ParentCategoryId);

            await _categoryRepository.AddAsync(category, cancellationToken);

            return Result.Success(category.Id);
        }
    }
}
