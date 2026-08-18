namespace Kart.Review.Infrastructure.Persistence;

/// <summary>
/// Wire shapes for the three published events — event-contract.md's approved payload fields,
/// verbatim field names/casing (camelCase, matching every other Kart service's JSON convention).
/// <c>EventType</c> is an addition beyond the approved contract's field list, embedded the same
/// way kart-identity-service's own outbox relay injects an <c>eventId</c> field into every
/// published body — a non-breaking addition (event consumers are unknown-field-tolerant per the
/// platform's own consumer-schema-compatibility convention) that lets THIS service's own REV-6
/// self-consumer (bound to all three routing keys on one queue) discriminate without relying on
/// routing-key plumbing the shared `RabbitMqConsumerHostedServiceBase` doesn't expose to
/// <c>ProcessAsync</c>.
/// </summary>
public sealed record ReviewSubmittedPayload(string EventType, Guid OrderId, string Sku, int Rating, Guid ReviewId, Guid UserId)
{
    public ReviewSubmittedPayload(Guid orderId, string sku, int rating, Guid reviewId, Guid userId)
        : this("ReviewSubmitted", orderId, sku, rating, reviewId, userId)
    {
    }
}

public sealed record ReviewUpdatedPayload(string EventType, Guid OrderId, string Sku, int OldRating, int NewRating)
{
    public ReviewUpdatedPayload(Guid orderId, string sku, int oldRating, int newRating)
        : this("ReviewUpdated", orderId, sku, oldRating, newRating)
    {
    }
}

public sealed record ReviewUnpublishedPayload(string EventType, Guid OrderId, string Sku, int Rating, Guid ReviewId, Guid UserId, string Reason)
{
    public ReviewUnpublishedPayload(Guid orderId, string sku, int rating, Guid reviewId, Guid userId, string reason)
        : this("ReviewUnpublished", orderId, sku, rating, reviewId, userId, reason)
    {
    }
}
