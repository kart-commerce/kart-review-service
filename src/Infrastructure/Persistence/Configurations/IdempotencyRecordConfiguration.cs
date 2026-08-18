using Kart.Review.Domain.Idempotency;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Kart.Review.Infrastructure.Persistence.Configurations;

/// <summary>
/// Maps <see cref="IdempotencyRecord"/> to `idempotency_keys` — PK `(idempotency_key, endpoint)`,
/// following kart-payment-service's precedent (a single, non-partitioned table; TTL cleanup is a
/// batched DELETE via <see cref="IdempotencyCleanupHostedService"/>, not a partition-drop — see
/// that class's own remarks for why partitioning was rejected).
/// </summary>
public sealed class IdempotencyRecordConfiguration : IEntityTypeConfiguration<IdempotencyRecord>
{
    public void Configure(EntityTypeBuilder<IdempotencyRecord> builder)
    {
        builder.ToTable("idempotency_keys");

        builder.HasKey(r => new { r.IdempotencyKey, r.Endpoint });

        builder.Property(r => r.IdempotencyKey).HasColumnName("idempotency_key").IsRequired();
        builder.Property(r => r.Endpoint)
            .HasColumnName("endpoint")
            .HasConversion(endpoint => endpoint.ToString(), value => Enum.Parse<IdempotencyEndpoint>(value))
            .IsRequired();

        builder.Property(r => r.RequestPayloadHash).HasColumnName("request_payload_hash").IsRequired();
        builder.Property(r => r.StoredResponse).HasColumnName("stored_response").HasColumnType("jsonb");
        builder.Property(r => r.CreatedAt).HasColumnName("created_at").IsRequired();
        builder.Property(r => r.ExpiresAt).HasColumnName("expires_at").IsRequired();
        builder.Property(r => r.UpdatedAt).HasColumnName("updated_at").IsRequired();
        builder.Property(r => r.CreatedBy).HasColumnName("created_by").IsRequired();
        builder.Property(r => r.UpdatedBy).HasColumnName("updated_by").IsRequired();

        builder.HasIndex(r => r.ExpiresAt).HasDatabaseName("idx_idempotency_keys_expiry");
    }
}
