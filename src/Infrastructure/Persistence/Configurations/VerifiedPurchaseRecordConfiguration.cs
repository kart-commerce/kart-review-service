using System.Text.Json;
using Kart.Review.Domain.Common.ValueObjects;
using Kart.Review.Domain.VerifiedPurchases;
using Kart.Review.Infrastructure.Persistence.Converters;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Kart.Review.Infrastructure.Persistence.Configurations;

/// <summary>Maps <see cref="VerifiedPurchaseRecord"/> to `verified_purchase_records` — database-design.md's DDL.</summary>
public sealed class VerifiedPurchaseRecordConfiguration : IEntityTypeConfiguration<VerifiedPurchaseRecord>
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public void Configure(EntityTypeBuilder<VerifiedPurchaseRecord> builder)
    {
        builder.ToTable("verified_purchase_records");

        builder.HasKey(r => r.OrderId);
        builder.Property(r => r.OrderId).HasColumnName("order_id").HasConversion(TypedIdValueConverters.For<OrderId>()).ValueGeneratedNever();

        builder.Property(r => r.UserId).HasColumnName("user_id")
            .HasConversion(
                userId => userId.HasValue ? userId.Value.Value : (Guid?)null,
                value => value.HasValue ? UserId.From(value.Value) : (UserId?)null);

        // database-design.md specifies a native Postgres `text[]` column, but Npgsql's own array
        // type mapping bypasses a plain scalar ValueConverter for array-typed properties (it
        // inspects the CLR property type directly rather than routing through the converter for
        // element mapping), surfacing as an InvalidCastException at save time. No SQL in this
        // service ever queries `skus` with an array operator (`ANY`/`@>`) — `GrantsAccessTo`
        // checks containment in memory after loading — so a JSON-encoded string column is
        // functionally equivalent and sidesteps that provider limitation entirely.
        builder.Property(r => r.Skus).HasColumnName("skus").HasColumnType("jsonb")
            .HasConversion(
                skus => JsonSerializer.Serialize(skus.Select(s => s.Value), JsonOptions),
                json => (IReadOnlyCollection<Sku>)JsonSerializer.Deserialize<string[]>(json, JsonOptions)!.Select(Sku.From).ToArray(),
                new ValueComparer<IReadOnlyCollection<Sku>>(
                    (a, b) => (a ?? Array.Empty<Sku>()).SequenceEqual(b ?? Array.Empty<Sku>()),
                    v => v.Aggregate(0, (hash, sku) => HashCode.Combine(hash, sku.GetHashCode())),
                    v => v.ToArray()));

        builder.Property(r => r.DeliveredAt).HasColumnName("delivered_at");
        builder.Property(r => r.CreatedAt).HasColumnName("created_at").IsRequired();
        builder.Property(r => r.UpdatedAt).HasColumnName("updated_at").IsRequired();
        builder.Property(r => r.CreatedBy).HasColumnName("created_by").IsRequired();
        builder.Property(r => r.UpdatedBy).HasColumnName("updated_by").IsRequired();
    }
}
