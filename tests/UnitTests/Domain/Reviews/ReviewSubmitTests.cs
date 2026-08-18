using FluentAssertions;
using Kart.Review.Domain.Common.ValueObjects;
using Kart.Review.Domain.Reviews;
using Kart.Review.Domain.Reviews.Events;
using ReviewEntity = Kart.Review.Domain.Reviews.Review;
using Xunit;

namespace Kart.Review.UnitTests.Domain.Reviews;

public sealed class ReviewSubmitTests
{
    private static readonly DateTimeOffset Now = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
    private static readonly OrderId AnOrderId = OrderId.From(Guid.NewGuid());
    private static readonly Sku ASku = Sku.From("SKU-1");
    private static readonly UserId AUserId = UserId.From(Guid.NewGuid());

    [Fact]
    public void Submit_ClearedByClassifier_PublishesImmediatelyAndRaisesReviewSubmitted()
    {
        var review = ReviewEntity.Submit(AnOrderId, ASku, AUserId, Rating.From(5), "Great product", "key-1", clearedByClassifier: true, Now);

        review.Status.Should().Be(ModerationStatus.Published);
        review.FirstPublishedAt.Should().Be(Now);
        review.DomainEvents.Should().ContainSingle().Which.Should().BeOfType<ReviewSubmittedDomainEvent>();
    }

    [Fact]
    public void Submit_FlaggedByClassifier_QueuesForModerationAndRaisesNoEvent()
    {
        var review = ReviewEntity.Submit(AnOrderId, ASku, AUserId, Rating.From(1), "Bad", "key-1", clearedByClassifier: false, Now);

        review.Status.Should().Be(ModerationStatus.PendingModeration);
        review.FirstPublishedAt.Should().BeNull();
        review.DomainEvents.Should().BeEmpty("defer-until-outcome: nothing public to reflect yet");
    }

    [Fact]
    public void Submit_StampsAuthorAsCreatedByAndUpdatedBy()
    {
        var review = ReviewEntity.Submit(AnOrderId, ASku, AUserId, Rating.From(3), "ok", "key-1", true, Now);

        review.CreatedBy.Should().Be(AUserId.ToString());
        review.UpdatedBy.Should().Be(AUserId.ToString());
    }

    [Fact]
    public void IsAuthor_MatchesOnlyTheSubmittingUser()
    {
        var review = ReviewEntity.Submit(AnOrderId, ASku, AUserId, Rating.From(3), "ok", "key-1", true, Now);

        review.IsAuthor(AUserId).Should().BeTrue();
        review.IsAuthor(UserId.From(Guid.NewGuid())).Should().BeFalse();
    }
}
