using FinanceTracker.Application.Budgets;
using FinanceTracker.Domain.Budgets;
using FinanceTracker.Domain.Common;
using Microsoft.EntityFrameworkCore;

namespace FinanceTracker.Infrastructure.Persistence.Repositories
{
    public sealed class BudgetRepository : IBudgetRepository
    {
        private readonly FinanceTrackerDbContext _context;

        public BudgetRepository(FinanceTrackerDbContext context)
        {
            _context = context;
        }

        public Task<Budget?> GetByIdAsync(BudgetId id, CancellationToken cancellationToken = default) =>
            _context.Budgets.SingleOrDefaultAsync(b => b.Id == id, cancellationToken);

        // Compared field by field, not `b.Period == period` — EF Core's SQL
        // translation for a value object's own equality operator on a
        // ComplexProperty isn't guaranteed, but comparing its individual
        // mapped columns (Year, Month) always translates correctly.
        public Task<Budget?> GetByCategoryAndPeriodAsync(
            CategoryId categoryId, BudgetPeriod period, CancellationToken cancellationToken = default) =>
            _context.Budgets.SingleOrDefaultAsync(
                b => b.CategoryId == categoryId
                    && b.Period.Year == period.Year
                    && b.Period.Month == period.Month,
                cancellationToken);

        public async Task AddAsync(Budget budget, CancellationToken cancellationToken = default)
        {
            await _context.Budgets.AddAsync(budget, cancellationToken);
            await _context.SaveChangesAsync(cancellationToken);
        }

        public async Task UpdateAsync(Budget budget, CancellationToken cancellationToken = default)
        {
            _context.Budgets.Update(budget);
            await _context.SaveChangesAsync(cancellationToken);
        }
    }
}
