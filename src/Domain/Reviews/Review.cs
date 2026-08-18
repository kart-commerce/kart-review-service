using Kart.Review.Domain.Common.ValueObjects;
using Kart.Review.Domain.Reviews.Events;
using Kart.Shared.Domain;

namespace Kart.Review.Domain.Reviews;

/// <summary>
/// Aggregate root — the single authoritative write-model record of one customer's opinion of one
/// <c>(orderId, sku)</c> pair. Submission, edit, retraction, and moderation outcome all resolve
/// onto this one row (ddd-model.md). Raises <see cref="IDomainEvent"/>s that
/// <c>ReviewDbContext.SaveChangesAsync</c> translates into <see cref="ReviewOutboxEvent"/> rows in
/// the same transaction — never dispatched via an in-memory bus.
///
/// <c>FirstPublishedAt</c> is the single field governing every publish/removal event-firing
/// decision (ddd-model.md invariant #7): set exactly once, never reset. <c>ReviewSubmitted</c>
/// fires iff a transition sets it for the first time; <c>ReviewUpdated</c> fires iff it was
/// already set and the rating actually changed; <c>ReviewUnpublished</c> fires iff it was set
/// before removal.
/// </summary>
public sealed class Review : AggregateRoot
{
    private static readonly TimeSpan EditWindow = TimeSpan.FromDays(30);

    public ReviewId ReviewId => ReviewId.From(Id);

    public OrderId OrderId { get; private set; }

    public Sku Sku { get; private set; }

    public UserId UserId { get; private set; }

    public Rating Rating { get; private set; }

    public string BodyText { get; private set; } = string.Empty;

    public ModerationStatus Status { get; private set; }

    /// <summary>Set only while a Published review has a not-yet-decided staged edit awaiting a moderator (ddd-model.md's PendingRevision value object).</summary>
    public PendingRevision? PendingRevision { get; private set; }

    public DateTimeOffset? FirstPublishedAt { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset LastEditedAt { get; private set; }

    public DateTimeOffset? RetractedAt { get; private set; }

    public string IdempotencyKey { get; private set; } = string.Empty;

    public string CreatedBy { get; private set; } = string.Empty;

    public string UpdatedBy { get; private set; } = string.Empty;

    /// <summary>EF Core materialization only.</summary>
    private Review()
    {
    }

    /// <summary>
    /// REV-3: inserts a brand-new review. <paramref name="clearedByClassifier"/> is the synchronous
    /// content-safety classifier's decision (REV-1), already resolved by the caller before this
    /// factory runs — never called speculatively. Defer-until-outcome (design-decisions.md): a
    /// flagged/fail-safe submission is inserted as <see cref="ModerationStatus.PendingModeration"/>
    /// with no domain event raised at all (nothing public to reflect anywhere yet).
    /// </summary>
    public static Review Submit(
        OrderId orderId,
        Sku sku,
        UserId userId,
        Rating rating,
        string bodyText,
        string idempotencyKey,
        bool clearedByClassifier,
        DateTimeOffset now)
    {
        var actingPrincipal = userId.ToString();
        var review = new Review
        {
            Id = ReviewId.New().Value,
            OrderId = orderId,
            Sku = sku,
            UserId = userId,
            Rating = rating,
            BodyText = bodyText,
            IdempotencyKey = idempotencyKey,
            Status = clearedByClassifier ? ModerationStatus.Published : ModerationStatus.PendingModeration,
            CreatedAt = now,
            LastEditedAt = now,
            CreatedBy = actingPrincipal,
            UpdatedBy = actingPrincipal,
        };

        if (clearedByClassifier)
        {
            review.FirstPublishedAt = now;
            review.Raise(new ReviewSubmittedDomainEvent(review.ReviewId, orderId, sku, rating, userId, now));
        }

        return review;
    }

    public bool IsEditWindowOpen(DateTimeOffset now) => now - CreatedAt <= EditWindow;

    public bool IsAuthor(UserId candidate) => UserId == candidate;

    /// <summary>
    /// REV-9: author-only edit within the 30-day window. <paramref name="clearedByClassifier"/> is
    /// the classifier's decision against the *new* content, already resolved by the caller.
    /// Branching per ddd-model.md's Defer-until-outcome invariant, generalized to edits (see
    /// <see cref="EditOutcome"/> for what each result means).
    ///
    /// Design note (not explicit in the approved contract): when a partial edit (rating-only or
    /// bodyText-only, minProperties=1) lands on a Published review that already has a staged
    /// PendingRevision, the unset field is merged from that *staged* content, not from the
    /// currently-Published content — "latest-edit-wins" reads most naturally as the author
    /// iterating on their own most recent not-yet-decided submission, not silently reverting one
    /// field back to the old public value.
    /// </summary>
    public EditOutcome Edit(Rating? newRating, string? newBodyText, bool clearedByClassifier, string actingPrincipal, DateTimeOffset now)
    {
        if (Status == ModerationStatus.Retracted)
        {
            return EditOutcome.Terminal;
        }

        if (!IsEditWindowOpen(now))
        {
            return EditOutcome.WindowClosed;
        }

        if (Status is ModerationStatus.PendingModeration or ModerationStatus.Rejected)
        {
            Rating = newRating ?? Rating;
            BodyText = newBodyText ?? BodyText;
            Status = clearedByClassifier ? ModerationStatus.Published : ModerationStatus.PendingModeration;
            LastEditedAt = now;
            UpdatedBy = actingPrincipal;

            if (clearedByClassifier && FirstPublishedAt is null)
            {
                FirstPublishedAt = now;
                Raise(new ReviewSubmittedDomainEvent(ReviewId, OrderId, Sku, Rating, UserId, now));
            }

            return EditOutcome.Applied;
        }

        // Status == Published.
        var baselineRating = PendingRevision?.NewRating ?? Rating;
        var baselineBodyText = PendingRevision?.NewBodyText ?? BodyText;
        var mergedRating = newRating ?? baselineRating;
        var mergedBodyText = newBodyText ?? baselineBodyText;

        if (clearedByClassifier)
        {
            var oldRating = Rating;
            Rating = mergedRating;
            BodyText = mergedBodyText;
            PendingRevision = null;
            LastEditedAt = now;
            UpdatedBy = actingPrincipal;

            if (oldRating.Value != mergedRating.Value)
            {
                Raise(new ReviewUpdatedDomainEvent(OrderId, Sku, oldRating, mergedRating, now));
            }

            return EditOutcome.Applied;
        }

        PendingRevision = new PendingRevision(mergedBodyText, mergedRating, now);
        LastEditedAt = now;
        UpdatedBy = actingPrincipal;
        return EditOutcome.Staged;
    }

    /// <summary>REV-10: author-only soft-retract, any time, from any non-Retracted status. Idempotent — safe to call repeatedly without an Idempotency-Key.</summary>
    public RetractOutcome Retract(string actingPrincipal, DateTimeOffset now)
    {
        if (Status == ModerationStatus.Retracted)
        {
            return RetractOutcome.AlreadyRetracted;
        }

        var wasEverPublished = FirstPublishedAt is not null;

        Status = ModerationStatus.Retracted;
        RetractedAt = now;
        LastEditedAt = now;
        UpdatedBy = actingPrincipal;
        PendingRevision = null;

        if (wasEverPublished)
        {
            Raise(new ReviewUnpublishedDomainEvent(ReviewId, OrderId, Sku, Rating, UserId, UnpublishReason.AuthorRetraction, now));
        }

        return RetractOutcome.Applied;
    }

    /// <summary>
    /// REV-11: moderator accept/reject, resolved by the review's current state (ddd-model.md's
    /// Defer-until-outcome moderator-branch table). A repeat action against an already-resolved
    /// review is a guarded no-op, never re-applied.
    /// </summary>
    public ModerateOutcome Moderate(ModerationAction action, string actingPrincipal, DateTimeOffset now)
    {
        if (Status == ModerationStatus.Retracted)
        {
            return ModerateOutcome.Terminal;
        }

        if (Status == ModerationStatus.Rejected)
        {
            // Already resolved — Rejected is terminal with respect to further moderator action
            // (ddd-model.md invariant #8). The author's own edit path can still revive it; that is
            // Edit's concern, not Moderate's.
            return ModerateOutcome.NoOp;
        }

        if (Status == ModerationStatus.PendingModeration)
        {
            if (action == ModerationAction.Accept)
            {
                Status = ModerationStatus.Published;
                LastEditedAt = now;
                UpdatedBy = actingPrincipal;

                if (FirstPublishedAt is null)
                {
                    FirstPublishedAt = now;
                    Raise(new ReviewSubmittedDomainEvent(ReviewId, OrderId, Sku, Rating, UserId, now));
                }

                return ModerateOutcome.Applied;
            }

            Status = ModerationStatus.Rejected;
            LastEditedAt = now;
            UpdatedBy = actingPrincipal;
            return ModerateOutcome.Applied;
        }

        // Status == Published.
        if (PendingRevision is not null)
        {
            if (action == ModerationAction.Accept)
            {
                var oldRating = Rating;
                Rating = PendingRevision.NewRating;
                BodyText = PendingRevision.NewBodyText;
                PendingRevision = null;
                LastEditedAt = now;
                UpdatedBy = actingPrincipal;

                if (oldRating.Value != Rating.Value)
                {
                    Raise(new ReviewUpdatedDomainEvent(OrderId, Sku, oldRating, Rating, now));
                }

                return ModerateOutcome.Applied;
            }

            // Reject on a staged revision: "dissolved by construction" (edge-cases.md) — discard
            // the staged content, the currently-Published review is untouched, no event.
            PendingRevision = null;
            LastEditedAt = now;
            UpdatedBy = actingPrincipal;
            return ModerateOutcome.Applied;
        }

        if (action == ModerationAction.Reject)
        {
            // Post-hoc takedown of already-live content — this content WAS counted while live, so
            // (unlike every other Reject branch) this one does fire ReviewUnpublished.
            Status = ModerationStatus.Rejected;
            LastEditedAt = now;
            UpdatedBy = actingPrincipal;
            Raise(new ReviewUnpublishedDomainEvent(ReviewId, OrderId, Sku, Rating, UserId, UnpublishReason.ModeratorTakedown, now));
            return ModerateOutcome.Applied;
        }

        // Accept on an already-Published review with no staged revision — nothing to do.
        return ModerateOutcome.NoOp;
    }
}
