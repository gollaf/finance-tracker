using FinanceTracker.Domain.Budgets;
using FinanceTracker.Domain.Categories;
using FinanceTracker.Domain.Common;
using FinanceTracker.Infrastructure.Persistence.Conversions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FinanceTracker.Infrastructure.Persistence.Configurations
{
    /// <summary>
    /// Maps Budget to its "Budgets" table. LimitAmount and Period are complex
    /// properties: their fields become columns on this table (ADR 0003).
    /// CategoryId has a foreign key but no navigation property (ADR 0005).
    /// </summary>
    /// <remarks>
    /// "One budget per category per period" is enforced by
    /// CreateBudgetCommandHandler, not by a unique index.
    /// </remarks>
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

            // Restrict: a Category with a Budget can't be deleted.
            builder.HasOne<Category>()
                .WithMany()
                .HasForeignKey(b => b.CategoryId)
                .OnDelete(DeleteBehavior.Restrict);
        }
    }
}
