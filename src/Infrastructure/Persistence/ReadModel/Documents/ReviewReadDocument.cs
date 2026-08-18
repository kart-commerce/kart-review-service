using MongoDB.Bson.Serialization.Attributes;

namespace Kart.Review.Infrastructure.Persistence.ReadModel.Documents;

/// <summary>
/// database-design.md's Read Model section, verbatim shape — the `review_read_model` collection
/// `GET /v1/reviews` (REV-8) serves from, never PostgreSQL directly. <see cref="AuthorDisplayName"/>
/// is a denormalized display name, never the raw <c>userId</c> (BRD §24.1.5's column-level
/// security rule: public reads project a display name only).
/// </summary>
public sealed class ReviewReadDocument
{
    [BsonId]
    public Guid Id { get; set; }

    public Guid OrderId { get; set; }

    public string Sku { get; set; } = string.Empty;

    public string AuthorDisplayName { get; set; } = string.Empty;

    public int Rating { get; set; }

    public string BodyText { get; set; } = string.Empty;

    public DateTimeOffset FirstPublishedAt { get; set; }

    public DateTimeOffset LastEditedAt { get; set; }
}
