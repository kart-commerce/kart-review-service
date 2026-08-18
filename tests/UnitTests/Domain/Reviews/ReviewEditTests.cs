using FluentAssertions;
using Kart.Review.Domain.Common.ValueObjects;
using Kart.Review.Domain.Reviews;
using Kart.Review.Domain.Reviews.Events;
using ReviewEntity = Kart.Review.Domain.Reviews.Review;
using Xunit;

namespace Kart.Review.UnitTests.Domain.Reviews;

public sealed class ReviewEditTests
{
    private static readonly DateTimeOffset Now = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
    private static readonly OrderId AnOrderId = OrderId.From(Guid.NewGuid());
    private static readonly Sku ASku = Sku.From("SKU-1");
    private static readonly UserId AUserId = UserId.From(Guid.NewGuid());

    private static ReviewEntity Published(bool cleared = true) =>
        ReviewEntity.Submit(AnOrderId, ASku, AUserId, Rating.From(3), "original", "key-1", clearedByClassifier: true, Now);

    [Fact]
    public void Edit_PublishedContent_ClearedAndRatingChanged_AppliesImmediatelyAndRaisesReviewUpdated()
    {
        var review = Published();
        review.ClearDomainEvents();

        var outcome = review.Edit(Rating.From(5), "revised", clearedByClassifier: true, "author", Now.AddDays(1));

        outcome.Should().Be(EditOutcome.Applied);
        review.Rating.Value.Should().Be(5);
        review.BodyText.Should().Be("revised");
        review.DomainEvents.Should().ContainSingle().Which.Should().BeOfType<ReviewUpdatedDomainEvent>();
    }

    [Fact]
    public void Edit_PublishedContent_ClearedButRatingUnchanged_AppliesButRaisesNoEvent()
    {
        var review = Published();
        review.ClearDomainEvents();

        var outcome = review.Edit(newRating: null, "revised text only", clearedByClassifier: true, "author", Now.AddDays(1));

        outcome.Should().Be(EditOutcome.Applied);
        review.BodyText.Should().Be("revised text only");
        review.DomainEvents.Should().BeEmpty("ReviewUpdated only fires for a rating-affecting change");
    }

    [Fact]
    public void Edit_PublishedContent_Flagged_StagesAsPendingRevisionWithoutTouchingPublishedContent()
    {
        var review = Published();
        review.ClearDomainEvents();

        var outcome = review.Edit(Rating.From(1), "flagged content", clearedByClassifier: false, "author", Now.AddDays(1));

        outcome.Should().Be(EditOutcome.Staged);
        review.Rating.Value.Should().Be(3, "the Published content must stay untouched while staged");
        review.BodyText.Should().Be("original");
        review.PendingRevision.Should().NotBeNull();
        review.PendingRevision!.NewBodyText.Should().Be("flagged content");
        review.DomainEvents.Should().BeEmpty();
    }

    [Fact]
    public void Edit_SecondEditWhileOneAlreadyStaged_ReplacesTheStagedRevision_LatestEditWins()
    {
        var review = Published();
        review.Edit(Rating.From(1), "first flagged edit", clearedByClassifier: false, "author", Now.AddDays(1));

        review.Edit(newRating: null, "second flagged edit", clearedByClassifier: false, "author", Now.AddDays(2));

        review.PendingRevision!.NewBodyText.Should().Be("second flagged edit");
        review.PendingRevision.NewRating.Value.Should().Be(1, "the unset rating field merges from the previously staged revision, not the old Published value");
    }

    [Fact]
    public void Edit_PendingModerationReview_ClearedOnEdit_PublishesAndRaisesReviewSubmitted()
    {
        var review = ReviewEntity.Submit(AnOrderId, ASku, AUserId, Rating.From(2), "bad", "key-1", clearedByClassifier: false, Now);
        review.ClearDomainEvents();

        var outcome = review.Edit(Rating.From(4), "fixed", clearedByClassifier: true, "author", Now.AddHours(1));

        outcome.Should().Be(EditOutcome.Applied);
        review.Status.Should().Be(ModerationStatus.Published);
        review.FirstPublishedAt.Should().NotBeNull();
        review.DomainEvents.Should().ContainSingle().Which.Should().BeOfType<ReviewSubmittedDomainEvent>();
    }

    [Fact]
    public void Edit_RejectedReview_IsStillEditable_NothingWasEverPublished()
    {
        var review = ReviewEntity.Submit(AnOrderId, ASku, AUserId, Rating.From(1), "bad", "key-1", clearedByClassifier: false, Now);
        review.Moderate(ModerationAction.Reject, "moderator", Now.AddHours(1));
        review.ClearDomainEvents();

        var outcome = review.Edit(Rating.From(4), "fixed now", clearedByClassifier: true, "author", Now.AddHours(2));

        outcome.Should().Be(EditOutcome.Applied);
        review.Status.Should().Be(ModerationStatus.Published);
    }

    [Fact]
    public void Edit_AfterEditWindowCloses_ReturnsWindowClosed()
    {
        var review = Published();

        var outcome = review.Edit(Rating.From(5), "too late", clearedByClassifier: true, "author", Now.AddDays(31));

        outcome.Should().Be(EditOutcome.WindowClosed);
        review.Rating.Value.Should().Be(3, "a rejected edit must not mutate the review");
    }

    [Fact]
    public void Edit_RetractedReview_ReturnsTerminal()
    {
        var review = Published();
        review.Retract("author", Now.AddDays(1));

        var outcome = review.Edit(Rating.From(5), "resurrect?", clearedByClassifier: true, "author", Now.AddDays(2));

        outcome.Should().Be(EditOutcome.Terminal);
    }

    [Fact]
    public void IsEditWindowOpen_ExactlyAtThirtyDays_IsStillOpen()
    {
        var review = Published();
        review.IsEditWindowOpen(Now.AddDays(30)).Should().BeTrue();
        review.IsEditWindowOpen(Now.AddDays(30).AddSeconds(1)).Should().BeFalse();
    }
}
