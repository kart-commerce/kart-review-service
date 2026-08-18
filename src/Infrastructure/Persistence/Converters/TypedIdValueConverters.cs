using Kart.Review.Domain.Common.ValueObjects;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace Kart.Review.Infrastructure.Persistence.Converters;

/// <summary>One generic converter maps ANY <see cref="ITypedEntityId{TSelf}"/> to a <c>uuid</c> column — no per-ID-type converter needed. Same pattern as kart-identity-service/kart-shipping-service.</summary>
internal static class TypedIdValueConverters
{
    public static ValueConverter<TId, Guid> For<TId>() where TId : struct, ITypedEntityId<TId>
    {
        Func<Guid, TId> fromGuid = TId.From; // dodges CS8927 (static abstract member in an expression tree)
        return new ValueConverter<TId, Guid>(id => id.Value, value => fromGuid(value));
    }
}
