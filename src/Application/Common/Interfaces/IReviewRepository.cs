using Kart.Review.Domain.Common.ValueObjects;

namespace Kart.Review.Application.Common.Interfaces;

/// <summary>One repository for the <see cref="Domain.Reviews.Review"/> aggregate root (kart-conventions.md's Repository Pattern standard — never a generic per-table repository).</summary>
public interface IReviewRepository
{
    Task<Domain.Reviews.Review?> GetByIdAsync(Guid reviewId, CancellationToken cancellationToken);

    Task<Domain.Reviews.Review?> GetByOrderAndSkuAsync(OrderId orderId, Sku sku, CancellationToken cancellationToken);

    void Add(Domain.Reviews.Review review);
}
