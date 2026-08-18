using Kart.Shared.Domain;

namespace Kart.Review.Domain.Reviews;

/// <summary>
/// Transactional Outbox row for a <see cref="Review"/> domain event, written in the same
/// transaction as the triggering mutation (<c>ReviewDbContext.SaveChangesAsync</c>'s override).
/// Inherits <see cref="OutboxEventBase"/> (the <c>Kart.Shared.Domain</c> generalized shape) rather
/// than hand-rolling, per kart-user-service's precedent as the one real consumer of that base.
///
/// Carries THREE independent progress markers on the one row — mirroring kart-order-service's own
/// dual-poller pattern (<c>PublishedAt</c> for the RabbitMQ relay, <c>ProjectedAt</c> for its Mongo
/// projector) extended to a third consumer here, since this service has three independent
/// downstream reactions to the same event stream (REV-4/REV-5/REV-6, buildable in parallel,
/// sharing no code):
/// <list type="bullet">
/// <item><see cref="OutboxEventBase.PublishedAt"/> — relayed to RabbitMQ's <c>review.exchange</c> (REV-4, external consumers: Product/Analytics/Search).</item>
/// <item><see cref="ReadModelProjectedAt"/> — applied to the MongoDB <c>review_read_model</c> collection (REV-5).</item>
/// <item>The <c>ProductRating</c> projection (REV-6) does NOT use a marker on this row — ddd-model.md's tickets.md dependency chain has REV-6 depend on REV-4 (the relay), so it is a genuine RabbitMQ self-consumer of <c>review.exchange</c>, not a third poller here.</item>
/// </list>
/// </summary>
public sealed class ReviewOutboxEvent : OutboxEventBase
{
    public DateTimeOffset? ReadModelProjectedAt { get; private set; }

    public string? TraceParent { get; private set; }

    public string CreatedBy { get; private set; } = string.Empty;

    public DateTimeOffset UpdatedAt { get; private set; }

    public string UpdatedBy { get; private set; } = string.Empty;

    /// <summary>EF Core materialization only.</summary>
    private ReviewOutboxEvent()
    {
    }

    public static ReviewOutboxEvent Create(Guid reviewId, string eventType, string payloadJson, DateTimeOffset now, string? traceParent, string createdBy)
    {
        var outboxEvent = new ReviewOutboxEvent
        {
            Id = Guid.NewGuid(),
            AggregateId = reviewId,
            EventType = eventType,
            Payload = payloadJson,
            OccurredAt = now,
            TraceParent = traceParent,
            CreatedBy = createdBy,
            UpdatedAt = now,
            UpdatedBy = createdBy,
        };

        return outboxEvent;
    }

    public new void MarkPublished(DateTimeOffset publishedAt)
    {
        base.MarkPublished(publishedAt);
        UpdatedAt = publishedAt;
        UpdatedBy = Common.SystemPrincipals.OutboxPoller;
    }

    public void MarkReadModelProjected(DateTimeOffset projectedAt)
    {
        ReadModelProjectedAt = projectedAt;
        UpdatedAt = projectedAt;
        UpdatedBy = Common.SystemPrincipals.ReadModelProjector;
    }
}
