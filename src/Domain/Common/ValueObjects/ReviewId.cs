namespace Kart.Review.Domain.Common.ValueObjects;

/// <summary>The stable identifier assigned to a <see cref="Reviews.Review"/> at insert time — referenced by <c>ReviewSubmitted</c>'s payload and by <c>ProductRating</c>'s own idempotency ledger (ddd-model.md).</summary>
public readonly record struct ReviewId(Guid Value) : ITypedEntityId<ReviewId>
{
    public static ReviewId New() => new(Guid.NewGuid());

    public static ReviewId From(Guid value) => new(value);

    public override string ToString() => Value.ToString();
}
