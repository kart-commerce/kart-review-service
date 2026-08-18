using Kart.Review.Domain.ProductRatings;
using Kart.Review.Infrastructure.Persistence.Converters;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Kart.Review.Infrastructure.Persistence.Configurations;

/// <summary>Maps <see cref="ProductRatingLedgerEntry"/> to `product_rating_ledger` — database-design.md's DDL. PK `(order_id, sku)`, not a synthetic id.</summary>
public sealed class ProductRatingLedgerEntryConfiguration : IEntityTypeConfiguration<ProductRatingLedgerEntry>
{
    public void Configure(EntityTypeBuilder<ProductRatingLedgerEntry> builder)
    {
        builder.ToTable("product_rating_ledger", t => t.HasCheckConstraint(
            "ck_product_rating_ledger_rating",
            "last_applied_rating IS NULL OR last_applied_rating BETWEEN 1 AND 5"));

        builder.HasKey(e => new { e.OrderId, e.Sku });
        builder.Property(e => e.OrderId).HasColumnName("order_id").HasConversion(TypedIdValueConverters.For<Domain.Common.ValueObjects.OrderId>());
        builder.Property(e => e.Sku).HasColumnName("sku").HasConversion(DomainValueConverters.SkuConverter);

        builder.Property(e => e.LastAppliedRating).HasColumnName("last_applied_rating").HasColumnType("smallint").HasConversion(DomainValueConverters.NullableRatingConverter);
        builder.Property(e => e.CreatedAt).HasColumnName("created_at").IsRequired();
        builder.Property(e => e.UpdatedAt).HasColumnName("updated_at").IsRequired();
        builder.Property(e => e.CreatedBy).HasColumnName("created_by").IsRequired();
        builder.Property(e => e.UpdatedBy).HasColumnName("updated_by").IsRequired();

        builder.HasOne<ProductRating>().WithMany().HasForeignKey(e => e.Sku).HasPrincipalKey(r => r.Sku);
    }
}
