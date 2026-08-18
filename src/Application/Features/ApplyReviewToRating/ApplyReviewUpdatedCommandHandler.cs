using Kart.Review.Application.Common.Interfaces;
using Kart.Review.Domain.Common;
using Kart.Review.Domain.Common.ValueObjects;
using Kart.Review.Domain.ProductRatings;
using Kart.Review.Domain.Reviews;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Kart.Review.Application.Features.ApplyReviewToRating;

public sealed class ApplyReviewUpdatedCommandHandler(
    IProductRatingRepository productRatings,
    IProductRatingLedgerRepository ledgers,
    IUnitOfWork unitOfWork,
    TimeProvider timeProvider,
    ILogger<ApplyReviewUpdatedCommandHandler> logger) : IRequestHandler<ApplyReviewUpdatedCommand>
{
    public async Task Handle(ApplyReviewUpdatedCommand request, CancellationToken cancellationToken)
    {
        var orderId = OrderId.From(request.OrderId);
        var sku = Sku.From(request.Sku);
        var oldRating = Rating.From(request.OldRating);
        var newRating = Rating.From(request.NewRating);
        var now = timeProvider.GetUtcNow();

        var productRating = await productRatings.GetOrCreateAsync(sku, now, SystemPrincipals.RatingProjector, cancellationToken);

        var ledger = await ledgers.GetOrCreateAsync(orderId, sku, now, SystemPrincipals.RatingProjector, cancellationToken);

        if (ledger.LastAppliedRating?.Value == newRating.Value)
        {
            logger.LogInformation("Stage {Stage}: ReviewUpdated for order {OrderId} sku {Sku} already reflected in ledger — no-op (redelivery)", "RatingProjectionNoOp", request.OrderId, request.Sku);
            return;
        }

        if (ledger.LastAppliedRating?.Value == oldRating.Value)
        {
            productRating.ApplyUpdated(oldRating, newRating, now, SystemPrincipals.RatingProjector);
        }
        else
        {
            // Defensive fallback: the ledger doesn't hold the expected prior rating (e.g. this
            // service's own ReviewSubmitted for the same (orderId, sku) was lost/still in its DLQ
            // when this ReviewUpdated arrived). Treat the new rating as a fresh contribution rather
            // than silently dropping it — an under-count is worse than a rare double-count here,
            // and DLQ replay of the original ReviewSubmitted will simply no-op once it does arrive
            // (ledger.LastAppliedRating will already be non-null by then).
            logger.LogWarning(
                "Stage {Stage}: ReviewUpdated for order {OrderId} sku {Sku} arrived with no matching prior ledger state (expected {OldRating}, found {LedgerRating}); applying {NewRating} as a fresh contribution",
                "RatingProjectionFallbackBranch", request.OrderId, request.Sku, oldRating.Value, ledger.LastAppliedRating?.Value, newRating.Value);
            productRating.ApplySubmitted(newRating, now, SystemPrincipals.RatingProjector);
        }

        ledger.SetLastAppliedRating(newRating, now, SystemPrincipals.RatingProjector);

        await unitOfWork.SaveChangesAsync(cancellationToken);

        logger.LogInformation("Stage {Stage}: ProductRating for sku {Sku} updated to avg={Avg} count={Count} from ReviewUpdated", "ReadModelPersisted", request.Sku, productRating.Avg, productRating.Count);
    }
}
