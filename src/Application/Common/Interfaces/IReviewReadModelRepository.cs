using Kart.Review.Application.Common.Models;

namespace Kart.Review.Application.Common.Interfaces;

/// <summary>
/// REV-8: <c>GET /v1/reviews</c> reads exclusively through here — the MongoDB
/// <c>review_read_model</c> collection, never PostgreSQL directly (BRD §7 CQRS). Also the write
/// side the REV-5 projector (<c>Infrastructure/Messaging/ReviewReadModelProjectionHostedService</c>)
/// uses to keep it in sync, rebuilding each document from current PostgreSQL state rather than
/// trusting the outbox row's own payload — "the read model must be rebuildable from the write
/// model" (PLATFORM_BLUEPRINT.md's CQRS standard).
/// </summary>
public interface IReviewReadModelRepository
{
    Task<(IReadOnlyList<PublicReviewResponse> Items, long TotalCount)> SearchBySkuAsync(string sku, int page, int pageSize, CancellationToken cancellationToken);

    Task UpsertAsync(PublicReviewResponse review, CancellationToken cancellationToken);

    Task DeleteAsync(Guid reviewId, CancellationToken cancellationToken);
}
