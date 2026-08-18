using FluentAssertions;
using Kart.Review.Domain.Common.ValueObjects;
using Kart.Review.Domain.ProductRatings;
using Kart.Review.Domain.Reviews;
using Xunit;

namespace Kart.Review.UnitTests.Domain.ProductRatings;

public sealed class ProductRatingTests
{
    private static readonly DateTimeOffset Now = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
    private static readonly Sku ASku = Sku.From("SKU-1");

    [Fact]
    public void ApplySubmitted_Once_SetsAverageToTheSingleRating()
    {
        var rating = ProductRating.CreateEmpty(ASku, Now, "system");

        rating.ApplySubmitted(Rating.From(4), Now, "system");

        rating.Count.Value.Should().Be(1);
        rating.Avg.Value.Should().Be(4);
    }

    [Fact]
    public void ApplySubmitted_MultipleTimes_ComputesTheTrueRunningAverage_NeverFullRecompute()
    {
        var rating = ProductRating.CreateEmpty(ASku, Now, "system");

        foreach (var value in new[] { 5, 3, 4 })
        {
            rating.ApplySubmitted(Rating.From(value), Now, "system");
        }

        rating.Count.Value.Should().Be(3);
        rating.Avg.Value.Should().BeApproximately(4.0, 0.0001, "(5+3+4)/3 = 4");
    }

    [Fact]
    public void ApplyUpdated_AdjustsAverageByTheDelta_CountUnchanged()
    {
        var rating = ProductRating.CreateEmpty(ASku, Now, "system");
        rating.ApplySubmitted(Rating.From(2), Now, "system");
        rating.ApplySubmitted(Rating.From(4), Now, "system"); // avg=3, count=2

        rating.ApplyUpdated(Rating.From(2), Rating.From(5), Now, "system"); // the first review's rating 2 -> 5

        rating.Count.Value.Should().Be(2, "an edit never changes the population size");
        rating.Avg.Value.Should().BeApproximately(4.5, 0.0001, "(5+4)/2 = 4.5");
    }

    [Fact]
    public void ApplyUnpublished_RemovesTheContributionAndDecrementsCount()
    {
        var rating = ProductRating.CreateEmpty(ASku, Now, "system");
        rating.ApplySubmitted(Rating.From(2), Now, "system");
        rating.ApplySubmitted(Rating.From(4), Now, "system");
        rating.ApplySubmitted(Rating.From(5), Now, "system"); // avg=(2+4+5)/3=3.6667, count=3

        rating.ApplyUnpublished(Rating.From(4), Now, "system");

        rating.Count.Value.Should().Be(2);
        rating.Avg.Value.Should().BeApproximately(3.5, 0.0001, "(2+5)/2 = 3.5");
    }

    [Fact]
    public void ApplyUnpublished_TheOnlyRating_ReturnsToZero()
    {
        var rating = ProductRating.CreateEmpty(ASku, Now, "system");
        rating.ApplySubmitted(Rating.From(5), Now, "system");

        rating.ApplyUnpublished(Rating.From(5), Now, "system");

        rating.Count.Value.Should().Be(0);
        rating.Avg.Value.Should().Be(0);
    }

    [Fact]
    public void RatingCount_NeverGoesNegative()
    {
        var count = RatingCount.Zero;
        count.Decrement().Value.Should().Be(0, "Decrement below zero must not underflow");
    }

    [Fact]
    public void RatingCount_From_RejectsNegativeValues()
    {
        var act = () => RatingCount.From(-1);
        act.Should().Throw<ArgumentOutOfRangeException>();
    }
}
