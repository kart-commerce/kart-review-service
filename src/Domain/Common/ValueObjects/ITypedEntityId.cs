namespace Kart.Review.Domain.Common.ValueObjects;

/// <summary>
/// CRTP marker every strongly-typed identity value object in this service implements, so a
/// single generic EF Core <c>ValueConverter</c> (see
/// <c>Infrastructure/Persistence/Converters/TypedIdValueConverters.cs</c>) can map any of them to
/// a <c>uuid</c> column without a per-type converter. Same pattern as
/// kart-identity-service/kart-shipping-service's own <c>Domain/ValueObjects/ITypedEntityId.cs</c>
/// — not yet promoted into <c>Kart.Shared.Domain</c>, so duplicated here rather than invented
/// differently.
/// </summary>
public interface ITypedEntityId<TSelf> where TSelf : struct, ITypedEntityId<TSelf>
{
    Guid Value { get; }

    static abstract TSelf From(Guid value);
}
