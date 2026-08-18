using Kart.Review.Domain.ProductRatings;

namespace Kart.Review.Application.Common.Models;

/// <summary>api-contract.yaml's <c>ProductRatingView</c> schema.</summary>
public sealed record ProductRatingResponse(string Sku, double Avg, int Count)
{
    public static ProductRatingResponse FromDomain(ProductRating rating) => new(rating.Sku.Value, rating.Avg.Value, rating.Count.Value);
}
