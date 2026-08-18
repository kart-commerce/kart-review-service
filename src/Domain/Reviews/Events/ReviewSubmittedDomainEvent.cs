using Kart.Review.Domain.Common.ValueObjects;
using Kart.Shared.Domain;

namespace Kart.Review.Domain.Reviews.Events;

/// <summary>
/// Fires exactly once per <see cref="Review"/> — the transition that first sets
/// <c>FirstPublishedAt</c> (a fresh cleared submission, an edit/moderation decision that clears a
/// previously-queued review). Payload per requirement-spec §6 Q5 / event-contract.md.
/// </summary>
public sealed record ReviewSubmittedDomainEvent(
    ReviewId ReviewId,
    OrderId OrderId,
    Sku Sku,
    Rating Rating,
    UserId UserId,
    DateTimeOffset OccurredAt) : IDomainEvent;
