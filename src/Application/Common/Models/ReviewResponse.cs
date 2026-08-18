using Kart.Review.Domain.Reviews;

namespace Kart.Review.Application.Common.Models;

/// <summary>api-contract.yaml's <c>ReviewView</c> schema, verbatim field-for-field.</summary>
public sealed record ReviewResponse(
    Guid ReviewId,
    Guid OrderId,
    string Sku,
    Guid UserId,
    int Rating,
    string BodyText,
    string Status,
    PendingRevisionResponse? PendingRevision,
    DateTimeOffset? FirstPublishedAt,
    DateTimeOffset CreatedAt,
    DateTimeOffset LastEditedAt,
    DateTimeOffset? RetractedAt)
{
    public static ReviewResponse FromDomain(Domain.Reviews.Review review) => new(
        review.ReviewId.Value,
        review.OrderId.Value,
        review.Sku.Value,
        review.UserId.Value,
        review.Rating.Value,
        review.BodyText,
        review.Status.ToString(),
        review.PendingRevision is null ? null : new PendingRevisionResponse(review.PendingRevision.NewBodyText, review.PendingRevision.NewRating.Value, review.PendingRevision.SubmittedAt),
        review.FirstPublishedAt,
        review.CreatedAt,
        review.LastEditedAt,
        review.RetractedAt);
}

/// <summary>api-contract.yaml's <c>PendingRevisionView</c> schema.</summary>
public sealed record PendingRevisionResponse(string NewBodyText, int NewRating, DateTimeOffset SubmittedAt);
