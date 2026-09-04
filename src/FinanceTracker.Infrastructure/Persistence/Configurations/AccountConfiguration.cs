using FinanceTracker.Domain.Accounts;
using FinanceTracker.Domain.Common;
using FinanceTracker.Infrastructure.Persistence.Conversions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FinanceTracker.Infrastructure.Persistence.Configurations
{
    /// <summary>
    /// Maps Account to its "Accounts" table. Two things here are new
    /// compared to CategoryConfiguration: Type is stored as text, not an
    /// integer (per ADR 0003 — readable in psql, immune to the enum's
    /// member order ever changing), and Name's max length is read off
    /// Account.MaxNameLength itself rather than a repeated magic number, so
    /// the column constraint can never quietly drift from the Domain
    /// invariant it's meant to mirror.
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
