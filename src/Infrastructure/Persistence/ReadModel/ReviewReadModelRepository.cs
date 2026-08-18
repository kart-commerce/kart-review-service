using Kart.Review.Application.Common.Interfaces;
using Kart.Review.Application.Common.Models;
using Kart.Review.Infrastructure.Persistence.ReadModel.Documents;
using MongoDB.Driver;

namespace Kart.Review.Infrastructure.Persistence.ReadModel;

/// <summary>REV-8's read path (`GET /v1/reviews`) and REV-5's write path (the projector) both go through here.</summary>
public sealed class ReviewReadModelRepository(ReviewReadDbContext readDbContext) : IReviewReadModelRepository
{
    public async Task<(IReadOnlyList<PublicReviewResponse> Items, long TotalCount)> SearchBySkuAsync(string sku, int page, int pageSize, CancellationToken cancellationToken)
    {
        var filter = Builders<ReviewReadDocument>.Filter.Eq(d => d.Sku, sku);

        var totalCount = await readDbContext.Reviews.CountDocumentsAsync(filter, cancellationToken: cancellationToken);

        var documents = await readDbContext.Reviews
            .Find(filter)
            .Sort(Builders<ReviewReadDocument>.Sort.Descending(d => d.FirstPublishedAt))
            .Skip((page - 1) * pageSize)
            .Limit(pageSize)
            .ToListAsync(cancellationToken);

        var items = documents.Select(ToResponse).ToList();
        return (items, totalCount);
    }

    public async Task UpsertAsync(PublicReviewResponse review, CancellationToken cancellationToken)
    {
        var document = new ReviewReadDocument
        {
            Id = review.ReviewId,
            OrderId = review.OrderId,
            Sku = review.Sku,
            AuthorDisplayName = review.AuthorDisplayName,
            Rating = review.Rating,
            BodyText = review.BodyText,
            FirstPublishedAt = review.FirstPublishedAt,
            LastEditedAt = review.LastEditedAt,
        };

        await readDbContext.Reviews.ReplaceOneAsync(
            d => d.Id == document.Id,
            document,
            new ReplaceOptions { IsUpsert = true },
            cancellationToken);
    }

    public async Task DeleteAsync(Guid reviewId, CancellationToken cancellationToken) =>
        await readDbContext.Reviews.DeleteOneAsync(d => d.Id == reviewId, cancellationToken);

    private static PublicReviewResponse ToResponse(ReviewReadDocument document) => new(
        document.Id, document.OrderId, document.Sku, document.AuthorDisplayName, document.Rating, document.BodyText, document.FirstPublishedAt, document.LastEditedAt);
}
