using Kart.Review.Application.Common.Interfaces;
using Kart.Review.Domain.Common;
using Kart.Review.Domain.Common.ValueObjects;
using Kart.Review.Domain.VerifiedPurchases;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Kart.Review.Application.Features.ConsumeOrderCreated;

/// <summary>Idempotent by construction (kart-identity-service's platform-wide at-least-once consumer convention) — re-applying the same payload is a no-op overwrite of the same values; no dedup ledger needed (ADR-0021).</summary>
public sealed class ConsumeOrderCreatedCommandHandler(
    IVerifiedPurchaseRepository verifiedPurchases,
    IUnitOfWork unitOfWork,
    TimeProvider timeProvider,
    ILogger<ConsumeOrderCreatedCommandHandler> logger) : IRequestHandler<ConsumeOrderCreatedCommand>
{
    public async Task Handle(ConsumeOrderCreatedCommand request, CancellationToken cancellationToken)
    {
        var orderId = OrderId.From(request.OrderId);
        var userId = UserId.From(request.UserId);
        var skus = request.Skus.Select(Sku.From).ToArray();
        var now = timeProvider.GetUtcNow();

        var record = await verifiedPurchases.GetByOrderIdAsync(orderId, cancellationToken);
        if (record is null)
        {
            record = VerifiedPurchaseRecord.CreateFromOrderCreated(orderId, userId, skus, now, SystemPrincipals.VerifiedPurchaseConsumer);
            verifiedPurchases.Add(record);
        }
        else
        {
            record.ApplyOrderCreated(userId, skus, now, SystemPrincipals.VerifiedPurchaseConsumer);
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);

        logger.LogInformation("Stage {Stage}: VerifiedPurchaseRecord upserted from OrderCreated for order {OrderId}", "ReadModelPersisted", request.OrderId);
    }
}
