using Kart.Review.Domain.Common.ValueObjects;
using Kart.Review.Domain.Reviews;

namespace Kart.Review.Domain.ProductRatings;

/// <summary>
/// Aggregate root — the canonical rating aggregate Review owns per ADR-0014, keyed by
/// <see cref="Sku"/>. Every other bounded context's own rating copy (Product's
/// <c>ratingSummary</c>, Search's <c>RatingSignal</c>) is a denormalized projection of THIS
/// aggregate's own event stream, not the other way around.
///
/// Updated only asynchronously (REV-6's RabbitMQ consumer), never in the same transaction as the
/// <see cref="Review"/> write that produced the triggering event. Does not inherit
/// <c>Kart.Shared.Domain.AggregateRoot</c> — it raises no domain events of its own (queried
/// directly via <c>GET /v1/product-ratings/{sku}</c>, not projected onto a read model) and is
/// keyed by <see cref="Sku"/> rather than a <c>Guid</c>, which that base does not support.
///
/// Idempotency for concurrent/redelivered events is NOT this class's concern — it exposes pure,
/// unconditional apply operations; the caller (the REV-6 consumer, via
/// <c>ProductRatingLedgerEntry</c>) is responsible for deciding whether a given event has already
/// been applied before calling one of these.
/// </summary>
public sealed class ProductRating
{
    public Sku Sku { get; private set; }

    public RatingAverage Avg { get; private set; }

    public RatingCount Count { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset UpdatedAt { get; private set; }

    public string CreatedBy { get; private set; } = string.Empty;

    public string UpdatedBy { get; private set; } = string.Empty;

    /// <summary>EF Core materialization only.</summary>
    private ProductRating()
    {
    }

    public static ProductRating CreateEmpty(Sku sku, DateTimeOffset now, string createdBy) => new()
    {
        Sku = sku,
        Avg = RatingAverage.Zero,
        Count = RatingCount.Zero,
        CreatedAt = now,
        UpdatedAt = now,
        CreatedBy = createdBy,
        UpdatedBy = createdBy,
    };

    public void ApplySubmitted(Rating rating, DateTimeOffset now, string updatedBy)
    {
        Count = Count.Increment();
        Avg = Avg.AdjustForNewRating(rating, Count);
        UpdatedAt = now;
        UpdatedBy = updatedBy;
    }

    public void ApplyUpdated(Rating oldRating, Rating newRating, DateTimeOffset now, string updatedBy)
    {
        Avg = Avg.AdjustForRatingChange(oldRating, newRating, Count);
        UpdatedAt = now;
        UpdatedBy = updatedBy;
    }

    public void ApplyUnpublished(Rating rating, DateTimeOffset now, string updatedBy)
    {
        var countBefore = Count;
        Count = Count.Decrement();
        Avg = Avg.AdjustForRemoval(rating, countBefore, Count);
        UpdatedAt = now;
        UpdatedBy = updatedBy;
    }
}
