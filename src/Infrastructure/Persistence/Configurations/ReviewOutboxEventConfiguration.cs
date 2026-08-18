using Kart.Review.Domain.Reviews;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Kart.Review.Infrastructure.Persistence.Configurations;

/// <summary>Maps <see cref="ReviewOutboxEvent"/> to `review_outbox` — database-design.md's DDL. The `event_type` CHECK constraint is a deliberate guardrail: nothing but the three approved event names can ever be written here.</summary>
public sealed class ReviewOutboxEventConfiguration : IEntityTypeConfiguration<ReviewOutboxEvent>
{
    public void Configure(EntityTypeBuilder<ReviewOutboxEvent> builder)
    {
        builder.ToTable("review_outbox", t => t.HasCheckConstraint(
            "ck_review_outbox_event_type",
            "event_type IN ('ReviewSubmitted', 'ReviewUpdated', 'ReviewUnpublished')"));

        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).HasColumnName("id").ValueGeneratedNever();

        builder.Property(e => e.AggregateId).HasColumnName("review_id").IsRequired();
        builder.Property(e => e.EventType).HasColumnName("event_type").HasColumnType("varchar(24)").IsRequired();
        builder.Property(e => e.Payload).HasColumnName("payload").HasColumnType("jsonb").IsRequired();
        builder.Property(e => e.OccurredAt).HasColumnName("created_at").IsRequired();
        builder.Property(e => e.PublishedAt).HasColumnName("published_at");
        builder.Property(e => e.ReadModelProjectedAt).HasColumnName("read_model_projected_at");
        builder.Property(e => e.TraceParent).HasColumnName("trace_parent");
        builder.Property(e => e.CreatedBy).HasColumnName("created_by").IsRequired();
        builder.Property(e => e.UpdatedAt).HasColumnName("updated_at").IsRequired();
        builder.Property(e => e.UpdatedBy).HasColumnName("updated_by").IsRequired();

        // Two partial indexes on the SAME column, one per independent poller — EF Core treats
        // repeated HasIndex(x => x.OccurredAt) calls as re-configuring a single index unless each
        // is given an explicit name via the HasIndex(expression, name) overload (not just
        // .HasDatabaseName() afterward), which is what actually keeps them as two distinct
        // indexes in the model — otherwise the second call silently overwrites the first.
        // The outbox relay poller's own scan target (REV-4).
        builder.HasIndex(e => e.OccurredAt, "idx_review_outbox_unpublished").HasFilter("published_at IS NULL");
        // The Mongo read-model projector's own scan target (REV-5) — independent progress marker on the same row.
        builder.HasIndex(e => e.OccurredAt, "idx_review_outbox_unprojected").HasFilter("read_model_projected_at IS NULL");
    }
}
