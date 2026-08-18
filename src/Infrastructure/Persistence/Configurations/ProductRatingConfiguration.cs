using Kart.Review.Domain.ProductRatings;
using Kart.Review.Infrastructure.Persistence.Converters;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Kart.Review.Infrastructure.Persistence.Configurations;

/// <summary>Maps <see cref="ProductRating"/> to `product_ratings` — database-design.md's DDL.</summary>
public sealed class ProductRatingConfiguration : IEntityTypeConfiguration<ProductRating>
{
    public void Configure(EntityTypeBuilder<ProductRating> builder)
    {
        builder.ToTable("product_ratings", t => t.HasCheckConstraint("ck_product_ratings_count", "count >= 0"));

        builder.HasKey(r => r.Sku);
        builder.Property(r => r.Sku).HasColumnName("sku").HasConversion(DomainValueConverters.SkuConverter).ValueGeneratedNever();

        builder.Property(r => r.Avg).HasColumnName("avg").HasColumnType("double precision")
            .HasConversion(avg => avg.Value, value => Domain.ProductRatings.RatingAverage.From(value))
            .IsRequired();
        builder.Property(r => r.Count).HasColumnName("count")
            .HasConversion(count => count.Value, value => Domain.ProductRatings.RatingCount.From(value))
            .IsRequired();
        builder.Property(r => r.CreatedAt).HasColumnName("created_at").IsRequired();
        builder.Property(r => r.UpdatedAt).HasColumnName("updated_at").IsRequired();
        builder.Property(r => r.CreatedBy).HasColumnName("created_by").IsRequired();
        builder.Property(r => r.UpdatedBy).HasColumnName("updated_by").IsRequired();
    }
}
