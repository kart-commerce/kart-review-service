using Kart.Review.Application.Common.Interfaces;
using Kart.Review.Domain.Common;
using Kart.Review.Domain.Common.ValueObjects;
using Kart.Review.Domain.VerifiedPurchases;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Kart.Review.Application.Features.ConsumeOrderDelivered;

/// <summary>
/// Same idempotent-upsert shape as <see cref="ConsumeOrderCreated.ConsumeOrderCreatedCommandHandler"/>.
/// RabbitMQ gives no cross-routing-key ordering guarantee even from the same publisher — this can
/// legitimately arrive before <c>OrderCreated</c> for the same order; that is not treated as a
/// failure here, the gate simply keeps rejecting "no matching delivered order found yet" until
/// both have landed (ADR-0021).
/// </summary>
public sealed class ConsumeOrderDeliveredCommandHandler(
    IVerifiedPurchaseRepository verifiedPurchases,
    IUnitOfWork unitOfWork,
    TimeProvider timeProvider,
    ILogger<ConsumeOrderDeliveredCommandHandler> logger) : IRequestHandler<ConsumeOrderDeliveredCommand>
{
    public async Task Handle(ConsumeOrderDeliveredCommand request, CancellationToken cancellationToken)
    {
        var orderId = OrderId.From(request.OrderId);
        var now = timeProvider.GetUtcNow();

        var record = await verifiedPurchases.GetByOrderIdAsync(orderId, cancellationToken);
        if (record is null)
        {
            record = VerifiedPurchaseRecord.CreateFromOrderDelivered(orderId, request.DeliveredAt, now, SystemPrincipals.VerifiedPurchaseConsumer);
            verifiedPurchases.Add(record);
        }
        else
        {
            record.ApplyOrderDelivered(request.DeliveredAt, now, SystemPrincipals.VerifiedPurchaseConsumer);
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);

        logger.LogInformation("Stage {Stage}: VerifiedPurchaseRecord upserted from OrderDelivered for order {OrderId}", "ReadModelPersisted", request.OrderId);
    }
}
