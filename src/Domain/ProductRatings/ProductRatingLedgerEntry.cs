using Kart.Review.Domain.Common.ValueObjects;
using Kart.Review.Domain.Reviews;

namespace Kart.Review.Domain.ProductRatings;

/// <summary>
/// The <c>(orderId, sku) -&gt; lastAppliedRating</c> idempotency ledger REV-6's consumer uses to
/// dedupe <c>ReviewSubmitted</c>/<c>ReviewUpdated</c>/<c>ReviewUnpublished</c> under RabbitMQ's
/// at-least-once redelivery (ddd-model.md's <c>ProcessedReviewLedger</c> value object). Keyed on
/// <c>(OrderId, Sku)</c> — not raw <c>ReviewId</c> — since <c>ReviewUpdated</c>'s payload lacks a
/// <c>reviewId</c>, and <c>(orderId, sku)</c> is 1:1 with it via <see cref="Review"/>'s own
/// uniqueness invariant. Retained indefinitely, never deleted, even after an unpublish — deleting
/// it would make a redelivered <c>ReviewUnpublished</c> indistinguishable from "never processed."
///
/// Not part of the <see cref="ProductRating"/> aggregate's loaded state (that would mean loading
/// every review ever written for a SKU on every single-event update) — a standalone entity a
/// dedicated repository locks and reads independently, one row at a time, inside the same
/// transaction as the <see cref="ProductRating"/> update it guards.
/// </summary>
public sealed class ProductRatingLedgerEntry
{
    public OrderId OrderId { get; private set; }

    public Sku Sku { get; private set; }

    /// <summary>Null once the contribution has been unpublished/retracted — a distinct, persisted state from "never applied."</summary>
    public Rating? LastAppliedRating { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset UpdatedAt { get; private set; }

    public string CreatedBy { get; private set; } = string.Empty;

    public string UpdatedBy { get; private set; } = string.Empty;

    /// <summary>EF Core materialization only.</summary>
    private ProductRatingLedgerEntry()
    {
    }

    public static ProductRatingLedgerEntry CreateEmpty(OrderId orderId, Sku sku, DateTimeOffset now, string principal) => new()
    {
        OrderId = orderId,
        Sku = sku,
        LastAppliedRating = null,
        CreatedAt = now,
        UpdatedAt = now,
        CreatedBy = principal,
        UpdatedBy = principal,
    };

    /// <summary>Per-event-type dedup rule (ddd-model.md): true when the incoming event's outcome is already reflected here — the caller must no-op rather than re-apply to <see cref="ProductRating"/>.</summary>
    public bool AlreadyReflects(Rating? expectedLastAppliedRating) => LastAppliedRating == expectedLastAppliedRating;

    public void SetLastAppliedRating(Rating? rating, DateTimeOffset now, string principal)
    {
        LastAppliedRating = rating;
        UpdatedAt = now;
        UpdatedBy = principal;
    }
}
