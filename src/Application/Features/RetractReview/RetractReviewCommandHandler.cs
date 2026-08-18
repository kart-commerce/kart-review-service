using Kart.Review.Application.Common.Exceptions;
using Kart.Review.Application.Common.Interfaces;
using Kart.Review.Application.Common.Models;
using Kart.Shared.Auditing;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Kart.Review.Application.Features.RetractReview;

/// <summary>REV-10: author-only soft-retract from any non-Retracted status; a repeat retraction against an already-Retracted review is a guarded no-op (<see cref="Domain.Reviews.Review.Retract"/>).</summary>
public sealed class RetractReviewCommandHandler(
    IReviewRepository reviews,
    IUnitOfWork unitOfWork,
    ICurrentPrincipal currentPrincipal,
    IAuditLogWriter auditLogWriter,
    TimeProvider timeProvider,
    ILogger<RetractReviewCommandHandler> logger) : IRequestHandler<RetractReviewCommand, ReviewResponse>
{
    public async Task<ReviewResponse> Handle(RetractReviewCommand request, CancellationToken cancellationToken)
    {
        logger.LogInformation("Stage {Stage}: RetractReview received for review {ReviewId}", "RetractReviewRequestReceived", request.ReviewId);

        var review = await reviews.GetByIdAsync(request.ReviewId, cancellationToken) ?? throw new ReviewNotFoundException(request.ReviewId);

        if (!review.IsAuthor(currentPrincipal.UserId))
        {
            throw new NotReviewAuthorException(request.ReviewId);
        }

        var outcome = review.Retract(currentPrincipal.PrincipalId, timeProvider.GetUtcNow());
        await unitOfWork.SaveChangesAsync(cancellationToken);

        logger.LogInformation("Stage {Stage}: review {ReviewId} retract {Outcome}", "ReviewPersisted", review.ReviewId, outcome);

        await auditLogWriter.WriteAsync(
            AuditLogEntry.Create("kart-review-service", currentPrincipal.PrincipalId, "user", "RetractReview", "Review", review.ReviewId.ToString()),
            cancellationToken);

        return ReviewResponse.FromDomain(review);
    }
}
