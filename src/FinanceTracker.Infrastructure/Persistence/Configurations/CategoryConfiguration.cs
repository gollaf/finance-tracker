using FinanceTracker.Domain.Categories;
using FinanceTracker.Domain.Common;
using FinanceTracker.Infrastructure.Persistence.Conversions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FinanceTracker.Infrastructure.Persistence.Configurations
{
    /// <summary>
    /// Maps Category to its "Categories" table. ParentCategoryId is mapped
    /// as a plain converted column, not an EF Core relationship, on
    /// purpose: Category has no navigation property to its parent (see
    /// docs/domain-model.md), and this configuration deliberately mirrors
    /// that boundary rather than quietly reintroducing the object-graph
    /// coupling the Domain model avoids. Whether cross-aggregate ID columns
    /// like this one should also get a database-level foreign key
    /// constraint (EF Core supports one without a navigation property) is a
    /// real decision, made once it can be illustrated with a genuinely
    /// cross-aggregate reference rather than this same-table self-reference.
    /// </summary>
    public sealed class CategoryConfiguration : IEntityTypeConfiguration<Category>
    {
        public void Configure(EntityTypeBuilder<Category> builder)
        {
            builder.ToTable("Categories");

            builder.HasKey(c => c.Id);

            builder.Property(c => c.Id)
                .HasConversion(new StronglyTypedIdValueConverter<CategoryId>(id => id.Value, value => new CategoryId(value)))
                .ValueGeneratedNever();

            // No HasMaxLength: Category.Name has no length invariant in the
            // Domain layer (unlike Account.Name), so the column stays an
            // unbounded Postgres `text` rather than inventing a constraint
            // Infrastructure has no business enforcing on its own.
            builder.Property(c => c.Name)
                .IsRequired();

            builder.Property(c => c.ParentCategoryId)
                .HasConversion(new StronglyTypedIdValueConverter<CategoryId>(id => id.Value, value => new CategoryId(value)));
        }
    }
}
