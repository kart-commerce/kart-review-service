namespace Kart.Review.Domain.Common;

/// <summary>
/// Well-known acting-principal identifiers stamped on <c>CreatedBy</c>/<c>UpdatedBy</c> (and audit
/// log entries) for every mutation performed by a background process rather than an authenticated
/// caller — never <c>NULL</c>, never client-suppliable (kart-requirements.md §24.3's audit-actor
/// invariant, restated in database-design.md).
/// </summary>
public static class SystemPrincipals
{
    public const string OutboxPoller = "system:review-outbox-poller";
    public const string RatingProjector = "system:review-rating-projector";
    public const string VerifiedPurchaseConsumer = "system:review-verified-purchase-consumer";
    public const string ReadModelProjector = "system:review-read-model-projector";
}
