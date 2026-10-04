using FinanceTracker.Infrastructure.Outbox;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FinanceTracker.Infrastructure.Persistence.Configurations
{
    /// <summary>
    /// Maps OutboxMessage to the "OutboxMessages" table. See
    /// docs/adr/0013-transactional-outbox.md.
    /// </summary>
    public sealed class OutboxMessageConfiguration : IEntityTypeConfiguration<OutboxMessage>
    {
        public void Configure(EntityTypeBuilder<OutboxMessage> builder)
        {
            builder.ToTable("OutboxMessages");

            builder.HasKey(m => m.Id);

            // Set by EfCoreOutbox, not the database: it is also the MessageId.
            builder.Property(m => m.Id)
                .ValueGeneratedNever();

            builder.Property(m => m.Type)
                .IsRequired()
                .HasMaxLength(OutboxMessage.MaxTypeLength);

            builder.Property(m => m.EventName)
                .IsRequired()
                .HasMaxLength(OutboxMessage.MaxEventNameLength);

            // jsonb, not text: invalid JSON is rejected, and it can be queried.
            builder.Property(m => m.Payload)
                .IsRequired()
                .HasColumnType("jsonb");

            builder.Property(m => m.OccurredAt)
                .IsRequired();

            builder.Property(m => m.LastError)
                .HasMaxLength(OutboxMessage.MaxLastErrorLength);

            // Partial index for the relay's query: it stays small however many
            // processed rows pile up.
            builder.HasIndex(m => m.OccurredAt)
                .HasDatabaseName("IX_OutboxMessages_Unprocessed")
                .HasFilter("\"ProcessedAt\" IS NULL");
        }
    }
}
