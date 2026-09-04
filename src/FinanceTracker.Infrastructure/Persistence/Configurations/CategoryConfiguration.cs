using FinanceTracker.Domain.Categories;
using FinanceTracker.Domain.Common;
using FinanceTracker.Infrastructure.Persistence.Conversions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FinanceTracker.Infrastructure.Persistence.Configurations
{
    /// <summary>
    /// Maps Category to its "Categories" table. ParentCategoryId is a plain
    /// converted column with no Domain navigation property — Category has
    /// no navigation to its parent (see docs/domain-model.md), and this
    /// configuration mirrors that boundary rather than reintroducing the
    /// object-graph coupling the Domain model avoids. It does get a real
    /// database-level foreign key constraint back onto Categories itself
    /// (self-referencing), configured without a navigation property, per
    /// ADR 0005.
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

            // Self-referencing foreign key, no navigation property on either
            // side (see ADR 0005). Restrict rather than Cascade/SetNull: a
            // Category with children must not be deletable by silently
            // orphaning or renumbering them.
            builder.HasOne<Category>()
                .WithMany()
                .HasForeignKey(c => c.ParentCategoryId)
                .OnDelete(DeleteBehavior.Restrict);
        }
    }
}
