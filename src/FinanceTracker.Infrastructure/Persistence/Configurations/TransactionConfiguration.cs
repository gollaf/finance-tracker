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
    /// Maps Transaction to its "Transactions" table. The last of the five
    /// aggregates, and the one with two real cross-aggregate references:
    /// AccountId (required) and CategoryId (optional). Both get real
    /// foreign key constraints with no Domain navigation property, same
    /// pattern as ADR 0005. Amount is a ComplexProperty, same pattern as
    /// Budget.LimitAmount (ADR 0003 and its amendment) — Transaction's
    /// constructor needed the same EF-Core-only secondary constructor
    /// Budget's did, for the same reason.
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

            // Nullable: a Transaction can be uncategorized (see
            // Transaction.Recategorize(null)). No IsRequired() call, unlike
            // AccountId above.
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

            // Restrict rather than Cascade/SetNull: an Account still backing
            // a Transaction must not be deletable by silently destroying
            // that transaction's history.
            builder.HasOne<Account>()
                .WithMany()
                .HasForeignKey(t => t.AccountId)
                .OnDelete(DeleteBehavior.Restrict);

            // Same reasoning for Category, even though the column is
            // nullable — a Category still referenced by an existing
            // Transaction must not be deletable by silently blanking that
            // Transaction's CategoryId.
            builder.HasOne<Category>()
                .WithMany()
                .HasForeignKey(t => t.CategoryId)
                .OnDelete(DeleteBehavior.Restrict);
        }
    }
}
