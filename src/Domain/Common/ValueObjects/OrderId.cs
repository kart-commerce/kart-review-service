namespace Kart.Review.Domain.Common.ValueObjects;

/// <summary>
/// ACL reference to an aggregate owned by kart-order-service — Review never redefines Order's own
/// vocabulary, only wraps the raw <c>Guid</c> reference field so it can never be accidentally
/// swapped for a <see cref="UserId"/> or <see cref="ReviewId"/> at a call site (primitive
/// obsession fix; ddd-model.md's ubiquitous-language "referenced-only" note).
/// </summary>
public readonly record struct OrderId(Guid Value) : ITypedEntityId<OrderId>
{
    public static OrderId From(Guid value) => new(value);

    public override string ToString() => Value.ToString();
}
