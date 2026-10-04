using FinanceTracker.Domain.Accounts;
using FinanceTracker.Domain.Categories;
using FinanceTracker.Domain.Common;
using FinanceTracker.Domain.Transactions;
using FinanceTracker.Infrastructure.Persistence.Conversions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FinanceTracker.Infrastructure.Persistence.Configurations
{
    /// <summary>
    /// Maps Transaction to its "Transactions" table. AccountId (required) and
    /// CategoryId (optional) are foreign keys with no navigation properties
    /// (ADR 0005); Amount is a complex property (ADR 0003).
    /// </summary>
    public sealed class TransactionConfiguration : IEntityTypeConfiguration<Transaction>
    {
        public void Configure(EntityTypeBuilder<Transaction> builder)
        {
            builder.ToTable("Transactions");

            builder.HasKey(t => t.Id);

            builder.Property(t => t.Id)
                .HasConversion(new StronglyTypedIdValueConverter<TransactionId>(id => id.Value, value => new TransactionId(value)))
                .ValueGeneratedNever();

            builder.Property(t => t.AccountId)
                .HasConversion(new StronglyTypedIdValueConverter<AccountId>(id => id.Value, value => new AccountId(value)))
                .IsRequired();

            // Nullable: a Transaction can be uncategorized.
            builder.Property(t => t.CategoryId)
                .HasConversion(new StronglyTypedIdValueConverter<CategoryId>(id => id.Value, value => new CategoryId(value)));

            builder.ComplexProperty(t => t.Amount, money =>
            {
                money.Property(m => m.Amount)
                    .HasColumnName("Amount")
                    .HasColumnType("numeric(18,2)")
                    .IsRequired();

                money.Property(m => m.Currency)
                    .HasColumnName("Currency")
                    .HasMaxLength(3)
                    .IsFixedLength()
                    .IsRequired();
            });

            builder.Property(t => t.Type)
                .HasConversion<string>()
                .HasMaxLength(10);

            builder.Property(t => t.Description)
                .IsRequired()
                .HasMaxLength(Transaction.MaxDescriptionLength);

            builder.Property(t => t.OccurredOn)
                .IsRequired();

            builder.Property(t => t.CreatedAt)
                .IsRequired();

            // Restrict: deleting an Account must not destroy its history.
            builder.HasOne<Account>()
                .WithMany()
                .HasForeignKey(t => t.AccountId)
                .OnDelete(DeleteBehavior.Restrict);

            // Restrict too, rather than silently clearing CategoryId.
            builder.HasOne<Category>()
                .WithMany()
                .HasForeignKey(t => t.CategoryId)
                .OnDelete(DeleteBehavior.Restrict);
        }
    }
}
