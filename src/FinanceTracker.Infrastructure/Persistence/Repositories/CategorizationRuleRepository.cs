using FinanceTracker.Application.Categorization;
using FinanceTracker.Domain.Categorization;
using Microsoft.EntityFrameworkCore;

namespace FinanceTracker.Infrastructure.Persistence.Repositories
{
    public sealed class CategorizationRuleRepository : ICategorizationRuleRepository
    {
        private readonly FinanceTrackerDbContext _context;

        public CategorizationRuleRepository(FinanceTrackerDbContext context)
        {
            _context = context;
        }

        // No ordering here on purpose — TransactionCategorizer.Categorize
        // already sorts by Priority itself, so returning rules in whatever
        // order Postgres gives them back would still produce correct
        // results. Ordering here too would just be redundant work.
        public async Task<IReadOnlyList<CategorizationRule>> GetAllAsync(CancellationToken cancellationToken = default) =>
            await _context.CategorizationRules.ToListAsync(cancellationToken);

        public async Task AddAsync(CategorizationRule rule, CancellationToken cancellationToken = default)
        {
            await _context.CategorizationRules.AddAsync(rule, cancellationToken);
            await _context.SaveChangesAsync(cancellationToken);
        }
    }
}
