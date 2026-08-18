namespace Kart.Review.Application.Common.Exceptions;

/// <summary>No published review exists yet for this SKU — no <c>ProductRating</c> row to return (api-contract.yaml's <c>GET /v1/product-ratings/{sku}</c> 404).</summary>
public sealed class ProductRatingNotFoundException(string sku) : Exception($"No product rating exists yet for sku '{sku}'.");
