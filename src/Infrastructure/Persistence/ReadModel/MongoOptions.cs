namespace Kart.Review.Infrastructure.Persistence.ReadModel;

/// <summary>Binds the `"Mongo"` config section.</summary>
public sealed class MongoOptions
{
    public const string SectionName = "Mongo";

    public string ConnectionString { get; set; } = string.Empty;

    public string Database { get; set; } = "kart_review_read";
}
