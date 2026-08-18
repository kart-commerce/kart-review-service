using Kart.Review.Domain.Common.ValueObjects;
using Kart.Review.Domain.ProductRatings;

namespace Kart.Review.Application.Common.Interfaces;

/// <summary>One repository for the <see cref="ProductRating"/> aggregate root, keyed by <see cref="Sku"/>.</summary>
public interface IProductRatingRepository
{
    Task<ProductRating?> GetBySkuAsync(Sku sku, CancellationToken cancellationToken);

    void Add(ProductRating rating);

    /// <summary>
    /// REV-6's entry point — ensures a <see cref="ProductRating"/> row exists (and is durably
    /// committed) for <paramref name="sku"/> before the caller creates any
    /// <c>ProductRatingLedgerEntry</c> referencing it via FK. Must be called, and its own save
    /// completed, before <see cref="IProductRatingLedgerRepository.GetOrCreateAsync"/> for the
    /// same sku — reversing that order trips `FK_product_rating_ledger_product_ratings_sku`.
    /// </summary>
    Task<ProductRating> GetOrCreateAsync(Sku sku, DateTimeOffset now, string principal, CancellationToken cancellationToken);
}
