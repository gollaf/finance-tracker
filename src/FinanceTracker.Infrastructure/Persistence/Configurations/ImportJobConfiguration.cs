using FinanceTracker.Domain.Accounts;
using FinanceTracker.Domain.Common;
using FinanceTracker.Domain.Imports;
using FinanceTracker.Infrastructure.Persistence.Conversions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FinanceTracker.Infrastructure.Persistence.Configurations
{
    /// <summary>
    /// Maps ImportJob to the "ImportJobs" table. Rows and Errors are jsonb
    /// columns rather than child tables: they are always loaded and saved
    /// together with their job and never queried on their own. See
    /// docs/adr/0016-asynchronous-csv-import.md.
    /// </summary>
    public sealed class ImportJobConfiguration : IEntityTypeConfiguration<ImportJob>
    {
        public void Configure(EntityTypeBuilder<ImportJob> builder)
        {
            builder.ToTable("ImportJobs");

            builder.HasKey(j => j.Id);

            builder.Property(j => j.Id)
                .HasConversion(new StronglyTypedIdValueConverter<ImportJobId>(id => id.Value, value => new ImportJobId(value)))
                .ValueGeneratedNever();

            builder.Property(j => j.AccountId)
                .HasConversion(new StronglyTypedIdValueConverter<AccountId>(id => id.Value, value => new AccountId(value)))
                .IsRequired();

            builder.Property(j => j.Status)
                .HasConversion<string>()
                .HasMaxLength(20);

            builder.Property(j => j.Rows)
                .HasJsonListConversion()
                .IsRequired();

            builder.Property(j => j.Errors)
                .HasJsonListConversion()
                .IsRequired();

            builder.Property(j => j.ImportedCount);

            builder.Property(j => j.FailureReason)
                .HasMaxLength(ImportJob.MaxFailureReasonLength);

            builder.Property(j => j.CreatedAt)
                .IsRequired();

            builder.Property(j => j.CompletedAt);

            // Same cross-aggregate reference rule as Transaction (ADR 0005):
            // a real foreign key, no navigation property, and an Account
            // with import history can't be deleted out from under it.
            builder.HasOne<Account>()
                .WithMany()
                .HasForeignKey(j => j.AccountId)
                .OnDelete(DeleteBehavior.Restrict);
        }
    }
}
