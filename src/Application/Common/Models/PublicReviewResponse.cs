namespace Kart.Review.Application.Common.Models;

/// <summary>
/// The public, read-model-served shape a review takes on <c>GET /v1/reviews</c> — deliberately
/// narrower than <see cref="ReviewResponse"/> (no <c>userId</c>, no <c>status</c>, no
/// <c>pendingRevision</c>/<c>retractedAt</c>): BRD §24.1.5's column-level security rule ("public
/// review reads return a display name only, never the raw user_id") plus the fact that a public
/// listing only ever contains reviews that are actually public right now (database-design.md's
/// Read Model section — insert on <c>ReviewSubmitted</c>, update in place on <c>ReviewUpdated</c>,
/// delete outright on <c>ReviewUnpublished</c>).
/// </summary>
public sealed record PublicReviewResponse(
    Guid ReviewId,
    Guid OrderId,
    string Sku,
    string AuthorDisplayName,
    int Rating,
    string BodyText,
    DateTimeOffset FirstPublishedAt,
    DateTimeOffset LastEditedAt);
