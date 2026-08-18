using FluentAssertions;
using Kart.Review.Domain.Common.ValueObjects;
using Kart.Review.Domain.Reviews;
using Kart.Review.Domain.Reviews.Events;
using ReviewEntity = Kart.Review.Domain.Reviews.Review;
using Xunit;

namespace Kart.Review.UnitTests.Domain.Reviews;

public sealed class ReviewRetractTests
{
    private static readonly DateTimeOffset Now = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
    private static readonly OrderId AnOrderId = OrderId.From(Guid.NewGuid());
    private static readonly Sku ASku = Sku.From("SKU-1");
    private static readonly UserId AUserId = UserId.From(Guid.NewGuid());

    [Fact]
    public void Retract_PublishedReview_FiresReviewUnpublishedWithAuthorRetractionReason()
    {
        var review = ReviewEntity.Submit(AnOrderId, ASku, AUserId, Rating.From(4), "great", "key-1", clearedByClassifier: true, Now);
        review.ClearDomainEvents();

        var outcome = review.Retract("author", Now.AddDays(1));

        outcome.Should().Be(RetractOutcome.Applied);
        review.Status.Should().Be(ModerationStatus.Retracted);
        review.RetractedAt.Should().Be(Now.AddDays(1));
        var evt = review.DomainEvents.Should().ContainSingle().Which.Should().BeOfType<ReviewUnpublishedDomainEvent>().Subject;
        evt.Reason.Should().Be(UnpublishReason.AuthorRetraction);
    }

    [Fact]
    public void Retract_NeverPublishedReview_RaisesNoEvent()
    {
        var review = ReviewEntity.Submit(AnOrderId, ASku, AUserId, Rating.From(1), "bad", "key-1", clearedByClassifier: false, Now);

        var outcome = review.Retract("author", Now.AddHours(1));

        outcome.Should().Be(RetractOutcome.Applied);
        review.DomainEvents.Should().BeEmpty("this content was never public, nothing to unpublish");
    }

    [Fact]
    public void Retract_AlreadyRetracted_IsAGuardedNoOp()
    {
        var review = ReviewEntity.Submit(AnOrderId, ASku, AUserId, Rating.From(4), "great", "key-1", true, Now);
        review.Retract("author", Now.AddDays(1));
        review.ClearDomainEvents();

        var outcome = review.Retract("author", Now.AddDays(2));

        outcome.Should().Be(RetractOutcome.AlreadyRetracted);
        review.DomainEvents.Should().BeEmpty();
    }

    [Fact]
    public void Retract_DiscardsAnyStagedPendingRevision()
    {
        var review = ReviewEntity.Submit(AnOrderId, ASku, AUserId, Rating.From(4), "great", "key-1", true, Now);
        review.Edit(Rating.From(1), "flagged edit", clearedByClassifier: false, "author", Now.AddDays(1));

        review.Retract("author", Now.AddDays(2));

        review.PendingRevision.Should().BeNull();
    }
}
