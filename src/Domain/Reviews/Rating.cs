namespace Kart.Review.Domain.Reviews;

/// <summary>
/// A 1..5 integer star value. "No BRD section or approved upstream doc states an explicit rating
/// scale; 1..5 is adopted as the industry-standard engineering default... revisable without any
/// structural change" (ddd-model.md). Wrapped as a value object — never a raw <c>int</c> — so an
/// out-of-range rating cannot exist anywhere past the API boundary (primitive-obsession fix the
/// database-design.md's own <c>CHECK (rating BETWEEN 1 AND 5)</c> already enforces at the storage
/// layer; this enforces the identical invariant in code).
/// </summary>
public readonly record struct Rating
{
    public const int MinValue = 1;
    public const int MaxValue = 5;

    public int Value { get; }

    private Rating(int value) => Value = value;

    public static Rating From(int value)
    {
        if (!TryFrom(value, out var rating))
        {
            throw new ArgumentOutOfRangeException(nameof(value), value, $"Rating must be between {MinValue} and {MaxValue}.");
        }

        return rating;
    }

    public static bool TryFrom(int value, out Rating rating)
    {
        if (value is < MinValue or > MaxValue)
        {
            rating = default;
            return false;
        }

        rating = new Rating(value);
        return true;
    }

    public override string ToString() => Value.ToString();
}
