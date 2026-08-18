namespace Kart.Review.Domain.Common.ValueObjects;

/// <summary>
/// ACL reference to a SKU owned by kart-product-service. Wrapped (rather than left as a raw
/// <c>string</c>) so it can never be silently swapped for <see cref="Reviews.PendingRevision.NewBodyText"/>
/// or any other free-text field at a call site, and so "what counts as a valid SKU reference"
/// (non-empty, trimmed) is enforced in exactly one place.
/// </summary>
public readonly record struct Sku
{
    public string Value { get; }

    private Sku(string value) => Value = value;

    public static Sku From(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException("Sku must not be empty.", nameof(value));
        }

        return new Sku(value.Trim());
    }

    public override string ToString() => Value;
}
