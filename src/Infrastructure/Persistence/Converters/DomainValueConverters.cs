using System.Text.Json;
using Kart.Review.Domain.Common.ValueObjects;
using Kart.Review.Domain.Reviews;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace Kart.Review.Infrastructure.Persistence.Converters;

/// <summary>Value converters for the domain's non-ID value objects — the primitive-obsession fix applied consistently at the persistence boundary too, not just in code.</summary>
internal static class DomainValueConverters
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public static readonly ValueConverter<Sku, string> SkuConverter = new(sku => sku.Value, value => Sku.From(value));

    public static readonly ValueConverter<Rating, int> RatingConverter = new(rating => rating.Value, value => Rating.From(value));

    public static readonly ValueConverter<Rating?, int?> NullableRatingConverter = new(
        rating => rating.HasValue ? rating.Value.Value : null,
        value => value.HasValue ? Rating.From(value.Value) : null);

    // PendingRevision itself embeds the Rating value object, which System.Text.Json cannot
    // construct directly (private constructor, no settable properties) — a small storage-only DTO
    // avoids needing a custom JsonConverter just for this one JSONB column.
    private sealed record PendingRevisionDto(string NewBodyText, int NewRating, DateTimeOffset SubmittedAt);

    public static readonly ValueConverter<PendingRevision?, string?> PendingRevisionConverter = new(
        revision => revision == null ? null : JsonSerializer.Serialize(new PendingRevisionDto(revision.NewBodyText, revision.NewRating.Value, revision.SubmittedAt), JsonOptions),
        json => Deserialize(json));

    private static PendingRevision? Deserialize(string? json)
    {
        if (string.IsNullOrEmpty(json))
        {
            return null;
        }

        var dto = JsonSerializer.Deserialize<PendingRevisionDto>(json, JsonOptions)!;
        return new PendingRevision(dto.NewBodyText, Rating.From(dto.NewRating), dto.SubmittedAt);
    }
}
