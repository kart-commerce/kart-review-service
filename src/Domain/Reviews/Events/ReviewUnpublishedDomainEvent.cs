using Kart.Review.Domain.Common.ValueObjects;
using Kart.Shared.Domain;

namespace Kart.Review.Domain.Reviews.Events;

/// <summary>Fires when a previously-public review stops being public — author retraction or moderator post-hoc takedown — iff <c>FirstPublishedAt</c> was already set (never for content that was queued/rejected without ever going live).</summary>
public sealed record ReviewUnpublishedDomainEvent(
    ReviewId ReviewId,
    OrderId OrderId,
    Sku Sku,
    Rating Rating,
    UserId UserId,
    UnpublishReason Reason,
    DateTimeOffset OccurredAt) : IDomainEvent;
