using Kart.Review.Domain.Common.ValueObjects;

namespace Kart.Review.Domain.VerifiedPurchases;

/// <summary>
/// Local, <c>orderId</c>-keyed lookup projection (not an aggregate root — no domain events, no
/// invariant beyond field commutativity) that <c>POST /v1/reviews</c>'s hard eligibility gate
/// reads synchronously. Populated by consuming <c>OrderCreated</c> (<see cref="UserId"/>,
/// <see cref="Skus"/>) and <c>OrderDelivered</c> (<see cref="DeliveredAt"/>) — two events, since
/// <c>OrderDelivered</c> alone carries neither field (ADR-0021).
///
/// Both upserts are full-field and commute regardless of arrival order — no dedup ledger needed
/// (ADR-0021's ordering-race resolution: RabbitMQ gives no cross-routing-key ordering guarantee,
/// so <c>OrderDelivered</c> can be consumed before <c>OrderCreated</c>; the gate simply keeps
/// rejecting "no matching delivered order found yet" until both have landed).
/// </summary>
public sealed class VerifiedPurchaseRecord
{
    public OrderId OrderId { get; private set; }

    public UserId? UserId { get; private set; }

    public IReadOnlyCollection<Sku> Skus { get; private set; } = Array.Empty<Sku>();

    public DateTimeOffset? DeliveredAt { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset UpdatedAt { get; private set; }

    public string CreatedBy { get; private set; } = string.Empty;

    public string UpdatedBy { get; private set; } = string.Empty;

    /// <summary>EF Core materialization only.</summary>
    private VerifiedPurchaseRecord()
    {
    }

    public static VerifiedPurchaseRecord CreateFromOrderCreated(OrderId orderId, UserId userId, IReadOnlyCollection<Sku> skus, DateTimeOffset now, string principal) => new()
    {
        OrderId = orderId,
        UserId = userId,
        Skus = skus,
        DeliveredAt = null,
        CreatedAt = now,
        UpdatedAt = now,
        CreatedBy = principal,
        UpdatedBy = principal,
    };

    public static VerifiedPurchaseRecord CreateFromOrderDelivered(OrderId orderId, DateTimeOffset deliveredAt, DateTimeOffset now, string principal) => new()
    {
        OrderId = orderId,
        UserId = null,
        Skus = Array.Empty<Sku>(),
        DeliveredAt = deliveredAt,
        CreatedAt = now,
        UpdatedAt = now,
        CreatedBy = principal,
        UpdatedBy = principal,
    };

    /// <summary>Idempotent by construction — re-applying the same OrderCreated payload is a no-op overwrite of the same values.</summary>
    public void ApplyOrderCreated(UserId userId, IReadOnlyCollection<Sku> skus, DateTimeOffset now, string principal)
    {
        UserId = userId;
        Skus = skus;
        UpdatedAt = now;
        UpdatedBy = principal;
    }

    /// <summary>Idempotent by construction — re-applying the same OrderDelivered payload is a no-op overwrite of the same value.</summary>
    public void ApplyOrderDelivered(DateTimeOffset deliveredAt, DateTimeOffset now, string principal)
    {
        DeliveredAt = deliveredAt;
        UpdatedAt = now;
        UpdatedBy = principal;
    }

    /// <summary>The exact eligibility gate <c>SubmitReview</c> checks (requirement-spec §6): a record must exist, be delivered, belong to this caller, and cover this SKU.</summary>
    public bool GrantsAccessTo(UserId candidateUserId, Sku sku) =>
        DeliveredAt is not null && UserId == candidateUserId && Skus.Contains(sku);
}
