using FinanceTracker.Domain.Categorization;
using FinanceTracker.Domain.Categories;
using FinanceTracker.Domain.Common;
using FinanceTracker.Infrastructure.Persistence.Conversions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FinanceTracker.Infrastructure.Persistence.Configurations
{
    /// <summary>
    /// Maps CategorizationRule to its "CategorizationRules" table. CategoryId
    /// has a foreign key but no navigation property (ADR 0005).
    /// </summary>
    public sealed class CategorizationRuleConfiguration : IEntityTypeConfiguration<CategorizationRule>
    {
        public void Configure(EntityTypeBuilder<CategorizationRule> builder)
        {
            builder.ToTable("CategorizationRules");

            builder.HasKey(r => r.Id);

            builder.Property(r => r.Id)
                .HasConversion(new StronglyTypedIdValueConverter<CategorizationRuleId>(id => id.Value, value => new CategorizationRuleId(value)))
                .ValueGeneratedNever();

            builder.Property(r => r.Pattern)
                .IsRequired();

            builder.Property(r => r.CategoryId)
                .HasConversion(new StronglyTypedIdValueConverter<CategoryId>(id => id.Value, value => new CategoryId(value)))
                .IsRequired();

            builder.Property(r => r.Priority)
                .IsRequired();

            // Restrict: a Category used by a rule can't be deleted.
            builder.HasOne<Category>()
                .WithMany()
                .HasForeignKey(r => r.CategoryId)
                .OnDelete(DeleteBehavior.Restrict);
        }
    }
}
