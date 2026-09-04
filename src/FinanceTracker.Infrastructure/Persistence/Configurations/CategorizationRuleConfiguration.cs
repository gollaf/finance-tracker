using FinanceTracker.Domain.Categorization;
using FinanceTracker.Domain.Common;
using FinanceTracker.Infrastructure.Persistence.Conversions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FinanceTracker.Infrastructure.Persistence.Configurations
{
    /// <summary>
    /// Maps CategorizationRule to its "CategorizationRules" table. CategoryId
    /// is a plain converted column, same reasoning as ParentCategoryId on
    /// CategoryConfiguration: no navigation property in the Domain model,
    /// so none is introduced here either.
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
        }
    }
}
