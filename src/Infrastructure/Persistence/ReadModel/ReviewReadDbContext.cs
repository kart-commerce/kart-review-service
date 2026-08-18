using Kart.Review.Infrastructure.Persistence.ReadModel.Documents;
using MongoDB.Driver;

namespace Kart.Review.Infrastructure.Persistence.ReadModel;

/// <summary>The CQRS read side — a thin typed-collection accessor over the Mongo database, mirroring kart-order-service's `OrderReadDbContext`.</summary>
public sealed class ReviewReadDbContext(IMongoDatabase database)
{
    public IMongoCollection<ReviewReadDocument> Reviews => database.GetCollection<ReviewReadDocument>("review_read_model");
}
