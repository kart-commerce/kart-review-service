using FluentAssertions;
using Kart.Review.Domain.Reviews;
using Xunit;

namespace Kart.Review.UnitTests.Domain.Reviews;

public sealed class RatingTests
{
    [Theory]
    [InlineData(1)]
    [InlineData(3)]
    [InlineData(5)]
    public void From_WithinRange_Succeeds(int value)
    {
        var rating = Rating.From(value);
        rating.Value.Should().Be(value);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(6)]
    [InlineData(-1)]
    public void From_OutOfRange_Throws(int value)
    {
        var act = () => Rating.From(value);
        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void TryFrom_OutOfRange_ReturnsFalse()
    {
        Rating.TryFrom(99, out _).Should().BeFalse();
    }
}
