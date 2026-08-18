using Kart.Review.Application.Common.Models;
using MediatR;

namespace Kart.Review.Application.Features.ListReviewsBySku;

/// <summary>REV-8: api-contract.yaml's <c>GET /v1/reviews</c> — served exclusively from the MongoDB read model, never PostgreSQL directly (BRD §7 CQRS).</summary>
public sealed record ListReviewsBySkuQuery(string Sku, int Page, int PageSize) : IRequest<ReviewListResponse>;
