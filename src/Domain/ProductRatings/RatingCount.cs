namespace Kart.Review.Domain.ProductRatings;

/// <summary>The current review count on a <see cref="ProductRating"/> — never negative.</summary>
public readonly record struct RatingCount
{
    public int Value { get; }

    private RatingCount(int value) => Value = value;

    public static RatingCount Zero => new(0);

    public static RatingCount From(int value)
    {
        if (value < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(value), value, "RatingCount cannot be negative.");
        }

        return new RatingCount(value);
    }

    public RatingCount Increment() => new(Value + 1);

    public RatingCount Decrement() => Value == 0 ? this : new RatingCount(Value - 1);

    public override string ToString() => Value.ToString();
}
