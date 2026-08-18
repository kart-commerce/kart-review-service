using Kart.Review.Domain.Common.ValueObjects;
using Kart.Review.Domain.VerifiedPurchases;

namespace Kart.Review.Application.Common.Interfaces;

/// <summary>Repository for the <see cref="VerifiedPurchaseRecord"/> lookup projection, keyed by <see cref="OrderId"/>.</summary>
public interface IVerifiedPurchaseRepository
{
    Task<VerifiedPurchaseRecord?> GetByOrderIdAsync(OrderId orderId, CancellationToken cancellationToken);

    void Add(VerifiedPurchaseRecord record);
}
