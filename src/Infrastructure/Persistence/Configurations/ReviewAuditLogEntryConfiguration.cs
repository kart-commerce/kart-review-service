using Kart.Review.Infrastructure.Auditing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Kart.Review.Infrastructure.Persistence.Configurations;

public sealed class ReviewAuditLogEntryConfiguration : IEntityTypeConfiguration<ReviewAuditLogEntry>
{
    public void Configure(EntityTypeBuilder<ReviewAuditLogEntry> builder)
    {
        builder.ToTable("audit_log");

        builder.HasKey(e => e.EntryId);
        builder.Property(e => e.EntryId).HasColumnName("entry_id").ValueGeneratedNever();

        builder.Property(e => e.ServiceName).HasColumnName("service_name").IsRequired();
        builder.Property(e => e.ActorId).HasColumnName("actor_id").IsRequired();
        builder.Property(e => e.ActorType).HasColumnName("actor_type").IsRequired();
        builder.Property(e => e.Action).HasColumnName("action").IsRequired();
        builder.Property(e => e.EntityType).HasColumnName("entity_type").IsRequired();
        builder.Property(e => e.EntityId).HasColumnName("entity_id").IsRequired();
        builder.Property(e => e.OccurredAt).HasColumnName("occurred_at").IsRequired();
        builder.Property(e => e.MetadataJson).HasColumnName("metadata").HasColumnType("jsonb");

        builder.HasIndex(e => new { e.EntityType, e.EntityId }).HasDatabaseName("idx_audit_log_entity");
    }
}
