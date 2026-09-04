using FinanceTracker.Domain.Budgets;
using FinanceTracker.Domain.Categories;
using FinanceTracker.Domain.Common;
using FinanceTracker.Infrastructure.Persistence.Conversions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FinanceTracker.Infrastructure.Persistence.Configurations
{
    /// <summary>
    /// Maps Budget to its "Budgets" table. First aggregate to use EF Core's
    /// ComplexProperty (EF Core 8+) rather than a plain converted scalar
    /// column: LimitAmount (Money) and Period (BudgetPeriod) are each more
    /// than one field, and ComplexProperty maps every field onto its own
    /// column on this same table — no separate table, no shadow key. See
    /// ADR 0003 (and its amendment on constructor binding).
    ///
    /// CategoryId is a plain converted column with no Domain navigation
    /// property, but does get a real database-level foreign key constraint
    /// back onto Categories, configured without a navigation property, per
    /// ADR 0005.
    ///
    /// Deliberately no unique index on (CategoryId, Period) yet. "One
    /// budget per category per period" is real (see docs/domain-model.md),
    /// but nothing here is under test driving it, and I'd rather add a
    /// database-level constraint alongside a test that actually proves it's
    /// enforced than guess at one now. The Application layer's
    /// GetByCategoryAndPeriodAsync check-before-insert is what currently
    /// backs the rule.
    /// </summary>
    public sealed class BudgetConfiguration : IEntityTypeConfiguration<Budget>
    {
        public void Configure(EntityTypeBuilder<Budget> builder)
        {
            builder.ToTable("Budgets");

            builder.HasKey(b => b.Id);

            builder.Property(b => b.Id)
                .HasConversion(new StronglyTypedIdValueConverter<BudgetId>(id => id.Value, value => new BudgetId(value)))
                .ValueGeneratedNever();

            builder.Property(b => b.CategoryId)
                .HasConversion(new StronglyTypedIdValueConverter<CategoryId>(id => id.Value, value => new CategoryId(value)))
                .IsRequired();

            builder.ComplexProperty(b => b.Period, period =>
            {
                period.Property(p => p.Year).HasColumnName("PeriodYear").IsRequired();
                period.Property(p => p.Month).HasColumnName("PeriodMonth").IsRequired();
            });

            builder.ComplexProperty(b => b.LimitAmount, money =>
            {
                money.Property(m => m.Amount)
                    .HasColumnName("LimitAmount")
                    .HasColumnType("numeric(18,2)")
                    .IsRequired();

                money.Property(m => m.Currency)
                    .HasColumnName("LimitCurrency")
                    .HasMaxLength(3)
                    .IsFixedLength()
                    .IsRequired();
            });

            // Restrict rather than Cascade/SetNull: a Category still backing
            // a Budget must not be deletable by silently orphaning the
            // budget.
            builder.HasOne<Category>()
                .WithMany()
                .HasForeignKey(b => b.CategoryId)
                .OnDelete(DeleteBehavior.Restrict);
        }
    }
}
