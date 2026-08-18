using Kart.Review.Infrastructure.Persistence.Converters;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Kart.Review.Infrastructure.Persistence.Configurations;

// Bare "Review" would resolve to the Kart.Review namespace segment itself (this project's root
// namespace) rather than the Domain.Reviews.Review type — even a using-alias loses to enclosing
// namespace member lookup, per C# name-resolution rules. "Domain.Reviews.Review" (relative,
// starting from "Domain" which is unambiguous) sidesteps it, matching the rest of this codebase.

/// <summary>Maps <see cref="Domain.Reviews.Review"/> to `reviews` — database-design.md's DDL, verbatim.</summary>
public sealed class ReviewConfiguration : IEntityTypeConfiguration<Domain.Reviews.Review>
{
    public void Configure(EntityTypeBuilder<Domain.Reviews.Review> builder)
    {
        builder.ToTable("reviews", t => t.HasCheckConstraint(
            "ck_reviews_status",
            "status IN ('PendingModeration', 'Published', 'Rejected', 'Retracted')"));

        builder.HasKey(r => r.Id);
        builder.Property(r => r.Id).HasColumnName("review_id").ValueGeneratedNever();

        builder.Property(r => r.OrderId).HasColumnName("order_id").HasConversion(TypedIdValueConverters.For<Domain.Common.ValueObjects.OrderId>()).IsRequired();
        builder.Property(r => r.Sku).HasColumnName("sku").HasConversion(DomainValueConverters.SkuConverter).IsRequired();
        builder.Property(r => r.UserId).HasColumnName("user_id").HasConversion(TypedIdValueConverters.For<Domain.Common.ValueObjects.UserId>()).IsRequired();
        builder.Property(r => r.Rating).HasColumnName("rating").HasColumnType("smallint").HasConversion(DomainValueConverters.RatingConverter).IsRequired();
        builder.Property(r => r.BodyText).HasColumnName("body_text").IsRequired();
        builder.Property(r => r.Status).HasColumnName("status").HasColumnType("varchar(20)").HasConversion<string>().IsRequired();
        builder.Property(r => r.PendingRevision).HasColumnName("pending_revision").HasColumnType("jsonb").HasConversion(DomainValueConverters.PendingRevisionConverter);
        builder.Property(r => r.FirstPublishedAt).HasColumnName("first_published_at");
        builder.Property(r => r.CreatedAt).HasColumnName("created_at").IsRequired();
        builder.Property(r => r.LastEditedAt).HasColumnName("last_edited_at").IsRequired();
        builder.Property(r => r.RetractedAt).HasColumnName("retracted_at");
        builder.Property(r => r.IdempotencyKey).HasColumnName("idempotency_key").IsRequired();
        builder.Property(r => r.CreatedBy).HasColumnName("created_by").IsRequired();
        builder.Property(r => r.UpdatedBy).HasColumnName("updated_by").IsRequired();

        // The one-review-per-order/SKU invariant (permanent occupancy, even in a terminal state)
        // and the creation-retry idempotency-key dedup — both database-design.md-mandated unique
        // constraints, independent of the IdempotencyBehaviour's own dedup-store layer.
        builder.HasIndex(r => new { r.OrderId, r.Sku }).IsUnique().HasDatabaseName("uq_reviews_order_id_sku");
        builder.HasIndex(r => r.IdempotencyKey).IsUnique().HasDatabaseName("uq_reviews_idempotency_key");

        // This index IS the moderation queue — no separate "moderation queue" aggregate exists
        // (ddd-model.md's Modeling Decision #9).
        builder.HasIndex(r => r.CreatedAt)
            .HasDatabaseName("idx_reviews_moderation_queue")
            .HasFilter("status = 'PendingModeration'");

        builder.Ignore(r => r.ReviewId);
        builder.Ignore(r => r.DomainEvents);
    }
}
