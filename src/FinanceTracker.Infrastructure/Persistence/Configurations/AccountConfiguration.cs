using FinanceTracker.Domain.Accounts;
using FinanceTracker.Domain.Common;
using FinanceTracker.Infrastructure.Persistence.Conversions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FinanceTracker.Infrastructure.Persistence.Configurations
{
    /// <summary>
    /// Maps Account to its "Accounts" table. Type is stored as text (see
    /// ADR 0003), and Name's max length comes from Account.MaxNameLength so
    /// the column can't drift from the Domain rule.
    /// </summary>
    public sealed class AccountConfiguration : IEntityTypeConfiguration<Account>
    {
        public void Configure(EntityTypeBuilder<Account> builder)
        {
            builder.ToTable("Accounts");

            builder.HasKey(a => a.Id);

            builder.Property(a => a.Id)
                .HasConversion(new StronglyTypedIdValueConverter<AccountId>(id => id.Value, value => new AccountId(value)))
                .ValueGeneratedNever();

            builder.Property(a => a.Name)
                .IsRequired()
                .HasMaxLength(Account.MaxNameLength);

            builder.Property(a => a.Type)
                .HasConversion<string>()
                .HasMaxLength(20);

            builder.Property(a => a.Currency)
                .IsRequired()
                .HasMaxLength(3)
                .IsFixedLength();

            builder.Property(a => a.IsClosed)
                .IsRequired();
        }
    }
}
