using FinanceTracker.Application.Categories;
using FinanceTracker.Application.Common;
using FinanceTracker.Domain.Categorization;
using FinanceTracker.Domain.Common;
using MediatR;

namespace FinanceTracker.Application.Categorization.CreateCategorizationRule
{
    public sealed class CreateCategorizationRuleCommandHandler
        : IRequestHandler<CreateCategorizationRuleCommand, Result<CategorizationRuleId>>
    {
        private readonly ICategoryRepository _categoryRepository;
        private readonly ICategorizationRuleRepository _categorizationRuleRepository;

        public CreateCategorizationRuleCommandHandler(
            ICategoryRepository categoryRepository, ICategorizationRuleRepository categorizationRuleRepository)
        {
            _categoryRepository = categoryRepository;
            _categorizationRuleRepository = categorizationRuleRepository;
        }

        public async Task<Result<CategorizationRuleId>> Handle(
            CreateCategorizationRuleCommand request, CancellationToken cancellationToken)
        {
            var category = await _categoryRepository.GetByIdAsync(request.CategoryId, cancellationToken);

            if (category is null)
            {
                return Result.Failure<CategorizationRuleId>(Error.NotFound(
                    "Category.NotFound", $"No category found with id '{request.CategoryId}'."));
            }

            var rule = CategorizationRule.Create(request.Pattern, request.CategoryId, request.Priority);

            await _categorizationRuleRepository.AddAsync(rule, cancellationToken);

            return Result.Success(rule.Id);
        }
    }
}
