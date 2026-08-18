using Microsoft.Extensions.Hosting;
using MongoDB.Driver;

namespace Kart.Review.Infrastructure.Persistence.ReadModel;

/// <summary>Ensures `review_read_model`'s compound index exists at startup — `db.review_read_model.createIndex({ sku: 1, firstPublishedAt: -1 })` (database-design.md), the exact shape REV-8's SKU-scoped, newest-first listing query needs.</summary>
public sealed class MongoIndexInitializerHostedService(ReviewReadDbContext readDbContext) : IHostedService
{
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        var indexKeys = Builders<Documents.ReviewReadDocument>.IndexKeys
            .Ascending(d => d.Sku)
            .Descending(d => d.FirstPublishedAt);

        await readDbContext.Reviews.Indexes.CreateOneAsync(
            new CreateIndexModel<Documents.ReviewReadDocument>(indexKeys, new CreateIndexOptions { Name = "idx_review_read_model_sku_published" }),
            cancellationToken: cancellationToken);
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
