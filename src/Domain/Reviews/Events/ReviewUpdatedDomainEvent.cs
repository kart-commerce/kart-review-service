using Kart.Review.Domain.Common.ValueObjects;
using Kart.Shared.Domain;

namespace Kart.Review.Domain.Reviews.Events;

/// <summary>Fires only for a rating-affecting content change to a review already public before the change (ddd-model.md). No <c>reviewId</c> in the payload — event-contract.md's approved shape is <c>orderId, sku, oldRating, newRating</c>, which is 1:1 with a <c>reviewId</c> via the <c>(order_id, sku)</c> uniqueness invariant.</summary>
public sealed record ReviewUpdatedDomainEvent(
    OrderId OrderId,
    Sku Sku,
    Rating OldRating,
    Rating NewRating,
    DateTimeOffset OccurredAt) : IDomainEvent;
