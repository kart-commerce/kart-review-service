namespace Kart.Review.Domain.Common.ValueObjects;

/// <summary>ACL reference to an aggregate owned by kart-identity-service — see <see cref="OrderId"/>'s remarks; the same primitive-obsession fix applies to <c>userId</c>.</summary>
public readonly record struct UserId(Guid Value) : ITypedEntityId<UserId>
{
    public static UserId From(Guid value) => new(value);

    public override string ToString() => Value.ToString();
}
