using Kart.Review.Application.Common.Interfaces;
using Kart.Review.Domain.Common;
using Kart.Review.Domain.Common.ValueObjects;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Kart.Review.Application.Features.ApplyReviewToRating;

public sealed class ApplyReviewUnpublishedCommandHandler(
    IProductRatingRepository productRatings,
    IProductRatingLedgerRepository ledgers,
    IUnitOfWork unitOfWork,
    TimeProvider timeProvider,
    ILogger<ApplyReviewUnpublishedCommandHandler> logger) : IRequestHandler<ApplyReviewUnpublishedCommand>
{
    public async Task Handle(ApplyReviewUnpublishedCommand request, CancellationToken cancellationToken)
    {
        var orderId = OrderId.From(request.OrderId);
        var sku = Sku.From(request.Sku);
        var now = timeProvider.GetUtcNow();

        // Ensures the FK'd parent row exists even in the (out-of-order-delivery) case where this
        // is the very first REV-6 event ever processed for this (orderId, sku) — RabbitMQ gives no
        // ordering guarantee across redeliveries, so ReviewUnpublished is not guaranteed to arrive
        // after ReviewSubmitted has already been consumed here, only after it was PUBLISHED.
        var productRating = await productRatings.GetOrCreateAsync(sku, now, SystemPrincipals.RatingProjector, cancellationToken);

        var ledger = await ledgers.GetOrCreateAsync(orderId, sku, now, SystemPrincipals.RatingProjector, cancellationToken);

        if (ledger.LastAppliedRating is null)
        {
            logger.LogInformation("Stage {Stage}: ReviewUnpublished for order {OrderId} sku {Sku} already reflected in ledger — no-op (redelivery or never applied)", "RatingProjectionNoOp", request.OrderId, request.Sku);
            return;
        }

        productRating.ApplyUnpublished(ledger.LastAppliedRating.Value, now, SystemPrincipals.RatingProjector);
        ledger.SetLastAppliedRating(null, now, SystemPrincipals.RatingProjector);

        await unitOfWork.SaveChangesAsync(cancellationToken);

        logger.LogInformation("Stage {Stage}: ProductRating for sku {Sku} updated to avg={Avg} count={Count} from ReviewUnpublished", "ReadModelPersisted", request.Sku, productRating.Avg, productRating.Count);
    }
}
