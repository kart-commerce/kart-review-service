using Kart.Review.Application.Common.Interfaces;
using Kart.Review.Domain.Common;
using Kart.Review.Domain.Common.ValueObjects;
using Kart.Review.Domain.ProductRatings;
using Kart.Review.Domain.Reviews;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Kart.Review.Application.Features.ApplyReviewToRating;

/// <summary>
/// Atomic incremental update, deduplicated by <c>(orderId, sku)</c> via the ledger — never a full
/// recompute, never a pessimistic per-SKU lock (design-decisions.md's Concurrency-Control
/// decision). <see cref="IProductRatingLedgerRepository.GetOrCreateAsync"/> hides the
/// concurrent-insert race; this handler only needs to decide whether the ledger already reflects
/// this event's outcome.
/// </summary>
public sealed class ApplyReviewSubmittedCommandHandler(
    IProductRatingRepository productRatings,
    IProductRatingLedgerRepository ledgers,
    IUnitOfWork unitOfWork,
    TimeProvider timeProvider,
    ILogger<ApplyReviewSubmittedCommandHandler> logger) : IRequestHandler<ApplyReviewSubmittedCommand>
{
    public async Task Handle(ApplyReviewSubmittedCommand request, CancellationToken cancellationToken)
    {
        var orderId = OrderId.From(request.OrderId);
        var sku = Sku.From(request.Sku);
        var rating = Rating.From(request.Rating);
        var now = timeProvider.GetUtcNow();

        // Must exist (and be durably committed) before the ledger entry below — the ledger row
        // has an FK to product_ratings(sku); creating it first trips that constraint.
        var productRating = await productRatings.GetOrCreateAsync(sku, now, SystemPrincipals.RatingProjector, cancellationToken);

        var ledger = await ledgers.GetOrCreateAsync(orderId, sku, now, SystemPrincipals.RatingProjector, cancellationToken);

        if (ledger.LastAppliedRating is not null)
        {
            logger.LogInformation("Stage {Stage}: ReviewSubmitted for order {OrderId} sku {Sku} already reflected in ledger — no-op (redelivery)", "RatingProjectionNoOp", request.OrderId, request.Sku);
            return;
        }

        productRating.ApplySubmitted(rating, now, SystemPrincipals.RatingProjector);
        ledger.SetLastAppliedRating(rating, now, SystemPrincipals.RatingProjector);

        await unitOfWork.SaveChangesAsync(cancellationToken);

        logger.LogInformation("Stage {Stage}: ProductRating for sku {Sku} updated to avg={Avg} count={Count} from ReviewSubmitted", "ReadModelPersisted", request.Sku, productRating.Avg, productRating.Count);
    }
}
