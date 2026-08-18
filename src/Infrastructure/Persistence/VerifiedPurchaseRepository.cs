using Kart.Review.Application.Common.Interfaces;
using Kart.Review.Domain.Common.ValueObjects;
using Kart.Review.Domain.VerifiedPurchases;
using Microsoft.EntityFrameworkCore;

namespace Kart.Review.Infrastructure.Persistence;

public sealed class VerifiedPurchaseRepository(ReviewDbContext dbContext) : IVerifiedPurchaseRepository
{
    public Task<VerifiedPurchaseRecord?> GetByOrderIdAsync(OrderId orderId, CancellationToken cancellationToken) =>
        dbContext.VerifiedPurchaseRecords.FirstOrDefaultAsync(r => r.OrderId == orderId, cancellationToken);

    public void Add(VerifiedPurchaseRecord record) => dbContext.VerifiedPurchaseRecords.Add(record);
}
