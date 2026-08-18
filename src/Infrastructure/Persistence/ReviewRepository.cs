using Kart.Review.Application.Common.Interfaces;
using Kart.Review.Domain.Common.ValueObjects;
using Microsoft.EntityFrameworkCore;

namespace Kart.Review.Infrastructure.Persistence;

public sealed class ReviewRepository(ReviewDbContext dbContext) : IReviewRepository
{
    public Task<Domain.Reviews.Review?> GetByIdAsync(Guid reviewId, CancellationToken cancellationToken) =>
        dbContext.Reviews.FirstOrDefaultAsync(r => r.Id == reviewId, cancellationToken);

    public Task<Domain.Reviews.Review?> GetByOrderAndSkuAsync(OrderId orderId, Sku sku, CancellationToken cancellationToken) =>
        dbContext.Reviews.FirstOrDefaultAsync(r => r.OrderId == orderId && r.Sku == sku, cancellationToken);

    public void Add(Domain.Reviews.Review review) => dbContext.Reviews.Add(review);
}
