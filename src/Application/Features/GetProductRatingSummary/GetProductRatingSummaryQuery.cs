using Kart.Review.Application.Common.Models;
using MediatR;

namespace Kart.Review.Application.Features.GetProductRatingSummary;

/// <summary>REV-7: api-contract.yaml's <c>GET /v1/product-ratings/{sku}</c> — reads the canonical <c>ProductRating</c> aggregate directly from PostgreSQL (no Mongo projection needed at this scale; database-design.md).</summary>
public sealed record GetProductRatingSummaryQuery(string Sku) : IRequest<ProductRatingResponse>;
