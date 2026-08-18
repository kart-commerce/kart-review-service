using FluentAssertions;
using Kart.Review.Domain.Common.ValueObjects;
using Kart.Review.Domain.Reviews;
using Kart.Review.Domain.Reviews.Events;
using ReviewEntity = Kart.Review.Domain.Reviews.Review;
using Xunit;

namespace Kart.Review.UnitTests.Domain.Reviews;

public sealed class ReviewModerateTests
{
    private static readonly DateTimeOffset Now = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
    private static readonly OrderId AnOrderId = OrderId.From(Guid.NewGuid());
    private static readonly Sku ASku = Sku.From("SKU-1");
    private static readonly UserId AUserId = UserId.From(Guid.NewGuid());

    [Fact]
    public void Moderate_AcceptPendingModeration_PublishesAndRaisesReviewSubmitted()
    {
        var review = ReviewEntity.Submit(AnOrderId, ASku, AUserId, Rating.From(2), "flagged", "key-1", clearedByClassifier: false, Now);

        var outcome = review.Moderate(ModerationAction.Accept, "moderator", Now.AddHours(1));

        outcome.Should().Be(ModerateOutcome.Applied);
        review.Status.Should().Be(ModerationStatus.Published);
        review.DomainEvents.Should().ContainSingle().Which.Should().BeOfType<ReviewSubmittedDomainEvent>();
    }

    [Fact]
    public void Moderate_RejectPendingModeration_TerminalRejectedWithNoEvent()
    {
        var review = ReviewEntity.Submit(AnOrderId, ASku, AUserId, Rating.From(1), "flagged", "key-1", clearedByClassifier: false, Now);

        var outcome = review.Moderate(ModerationAction.Reject, "moderator", Now.AddHours(1));

        outcome.Should().Be(ModerateOutcome.Applied);
        review.Status.Should().Be(ModerationStatus.Rejected);
        review.DomainEvents.Should().BeEmpty("never publicly visible, nothing to reflect");
    }

    [Fact]
    public void Moderate_AcceptStagedRevision_AppliesStagedContentAndRaisesReviewUpdated()
    {
        var review = ReviewEntity.Submit(AnOrderId, ASku, AUserId, Rating.From(3), "original", "key-1", true, Now);
        review.Edit(Rating.From(5), "revised", clearedByClassifier: false, "author", Now.AddDays(1));
        review.ClearDomainEvents();

        var outcome = review.Moderate(ModerationAction.Accept, "moderator", Now.AddDays(2));

        outcome.Should().Be(ModerateOutcome.Applied);
        review.Rating.Value.Should().Be(5);
        review.BodyText.Should().Be("revised");
        review.PendingRevision.Should().BeNull();
        review.DomainEvents.Should().ContainSingle().Which.Should().BeOfType<ReviewUpdatedDomainEvent>();
    }

    [Fact]
    public void Moderate_RejectStagedRevision_DiscardsItAndLeavesPublishedContentUntouched_DissolvedByConstruction()
    {
        var review = ReviewEntity.Submit(AnOrderId, ASku, AUserId, Rating.From(3), "original", "key-1", true, Now);
        review.Edit(Rating.From(5), "revised", clearedByClassifier: false, "author", Now.AddDays(1));
        review.ClearDomainEvents();

        var outcome = review.Moderate(ModerationAction.Reject, "moderator", Now.AddDays(2));

        outcome.Should().Be(ModerateOutcome.Applied);
        review.Rating.Value.Should().Be(3, "the Published content is untouched by a rejected staged revision");
        review.BodyText.Should().Be("original");
        review.PendingRevision.Should().BeNull();
        review.DomainEvents.Should().BeEmpty("edge-cases.md: dissolved by construction, nothing was ever counted");
    }

    [Fact]
    public void Moderate_PostHocRejectOnPublishedWithNoStagedRevision_TakesDownAndFiresReviewUnpublished()
    {
        var review = ReviewEntity.Submit(AnOrderId, ASku, AUserId, Rating.From(4), "abusive later flagged", "key-1", true, Now);
        review.ClearDomainEvents();

        var outcome = review.Moderate(ModerationAction.Reject, "moderator", Now.AddDays(1));

        outcome.Should().Be(ModerateOutcome.Applied);
        review.Status.Should().Be(ModerationStatus.Rejected);
        var evt = review.DomainEvents.Should().ContainSingle().Which.Should().BeOfType<ReviewUnpublishedDomainEvent>().Subject;
        evt.Reason.Should().Be(UnpublishReason.ModeratorTakedown);
    }

    [Fact]
    public void Moderate_AcceptOnPublishedWithNoStagedRevision_IsANoOp()
    {
        var review = ReviewEntity.Submit(AnOrderId, ASku, AUserId, Rating.From(4), "fine", "key-1", true, Now);
        review.ClearDomainEvents();

        var outcome = review.Moderate(ModerationAction.Accept, "moderator", Now.AddDays(1));

        outcome.Should().Be(ModerateOutcome.NoOp);
        review.DomainEvents.Should().BeEmpty();
    }

    [Fact]
    public void Moderate_AlreadyRejected_IsAGuardedNoOp_TerminalWithRespectToModeration()
    {
        var review = ReviewEntity.Submit(AnOrderId, ASku, AUserId, Rating.From(1), "flagged", "key-1", false, Now);
        review.Moderate(ModerationAction.Reject, "moderator", Now.AddHours(1));

        var outcome = review.Moderate(ModerationAction.Accept, "moderator", Now.AddHours(2));

        outcome.Should().Be(ModerateOutcome.NoOp);
        review.Status.Should().Be(ModerationStatus.Rejected);
    }

    [Fact]
    public void Moderate_RetractedReview_ReturnsTerminal()
    {
        var review = ReviewEntity.Submit(AnOrderId, ASku, AUserId, Rating.From(4), "fine", "key-1", true, Now);
        review.Retract("author", Now.AddDays(1));

        var outcome = review.Moderate(ModerationAction.Reject, "moderator", Now.AddDays(2));

        outcome.Should().Be(ModerateOutcome.Terminal);
    }
}
