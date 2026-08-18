using Kart.Review.Application.Common.Exceptions;
using Kart.Review.Application.Common.Interfaces;
using Kart.Review.Application.Common.Models;
using Kart.Review.Domain.Common.ValueObjects;
using Kart.Shared.Auditing;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Kart.Review.Application.Features.SubmitReview;

/// <summary>
/// REV-3: verified-purchase gate (synchronous, against Review's own local projection — never a
/// live call to Order), then the synchronous content-safety classifier (REV-1), then a
/// defer-until-outcome insert (ddd-model.md). The <c>(order_id, sku)</c> uniqueness invariant's
/// concurrent-race case (two requests for the same pair racing past the pre-check) surfaces here
/// as <see cref="DuplicateReviewException"/> re-thrown by <c>ReviewDbContext.SaveChangesAsync</c>'s
/// own unique-violation translation (Infrastructure), not duplicated in this handler.
/// </summary>
public sealed class SubmitReviewCommandHandler(
    IReviewRepository reviews,
    IVerifiedPurchaseRepository verifiedPurchases,
    IUnitOfWork unitOfWork,
    IContentSafetyClassifier classifier,
    ICurrentPrincipal currentPrincipal,
    IAuditLogWriter auditLogWriter,
    TimeProvider timeProvider,
    ILogger<SubmitReviewCommandHandler> logger) : IRequestHandler<SubmitReviewCommand, ReviewResponse>
{
    public async Task<ReviewResponse> Handle(SubmitReviewCommand request, CancellationToken cancellationToken)
    {
        logger.LogInformation("Stage {Stage}: SubmitReview received for order {OrderId} sku {Sku}", "SubmitReviewRequestReceived", request.OrderId, request.Sku);

        var orderId = OrderId.From(request.OrderId);
        var sku = Sku.From(request.Sku);
        var rating = Domain.Reviews.Rating.From(request.Rating);
        var userId = currentPrincipal.UserId;

        var verifiedPurchase = await verifiedPurchases.GetByOrderIdAsync(orderId, cancellationToken);
        if (verifiedPurchase is null || !verifiedPurchase.GrantsAccessTo(userId, sku))
        {
            logger.LogInformation("Stage {Stage}: VerifiedPurchase gate rejected order {OrderId} sku {Sku} for user {UserId}", "VerifiedPurchaseGateRejected", request.OrderId, request.Sku, userId);
            throw new VerifiedPurchaseNotFoundException();
        }

        var existing = await reviews.GetByOrderAndSkuAsync(orderId, sku, cancellationToken);
        if (existing is not null)
        {
            throw new DuplicateReviewException();
        }

        var classification = await classifier.ClassifyAsync(request.BodyText, cancellationToken);
        var cleared = classification == Common.Interfaces.ContentSafetyDecision.Cleared;
        logger.LogInformation("Stage {Stage}: content-safety classifier returned {Decision} branch for order {OrderId} sku {Sku}", "ContentSafetyClassifiedBranch", classification, request.OrderId, request.Sku);

        var now = timeProvider.GetUtcNow();
        var review = Domain.Reviews.Review.Submit(orderId, sku, userId, rating, request.BodyText, request.IdempotencyKey, cleared, now);

        reviews.Add(review);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        logger.LogInformation("Stage {Stage}: review {ReviewId} persisted with status {Status}", "ReviewPersisted", review.ReviewId, review.Status);

        await auditLogWriter.WriteAsync(
            AuditLogEntry.Create("kart-review-service", currentPrincipal.PrincipalId, "user", "SubmitReview", "Review", review.ReviewId.ToString()),
            cancellationToken);

        return ReviewResponse.FromDomain(review);
    }
}
