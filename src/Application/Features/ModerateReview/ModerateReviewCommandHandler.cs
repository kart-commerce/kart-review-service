using Kart.Review.Application.Common.Exceptions;
using Kart.Review.Application.Common.Interfaces;
using Kart.Review.Application.Common.Models;
using Kart.Review.Domain.Reviews;
using Kart.Shared.Auditing;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Kart.Review.Application.Features.ModerateReview;

/// <summary>REV-11: one endpoint covering three distinct contexts resolved by the review's current state (<see cref="Review.Moderate"/>'s own remarks for the full branch table).</summary>
public sealed class ModerateReviewCommandHandler(
    IReviewRepository reviews,
    IUnitOfWork unitOfWork,
    ICurrentPrincipal currentPrincipal,
    IAuditLogWriter auditLogWriter,
    TimeProvider timeProvider,
    ILogger<ModerateReviewCommandHandler> logger) : IRequestHandler<ModerateReviewCommand, ReviewResponse>
{
    public async Task<ReviewResponse> Handle(ModerateReviewCommand request, CancellationToken cancellationToken)
    {
        logger.LogInformation("Stage {Stage}: ModerateReview received for review {ReviewId} action {Action}", "ModerateReviewRequestReceived", request.ReviewId, request.Action);

        var review = await reviews.GetByIdAsync(request.ReviewId, cancellationToken) ?? throw new ReviewNotFoundException(request.ReviewId);

        var action = Enum.Parse<ModerationAction>(request.Action, ignoreCase: true);
        var outcome = review.Moderate(action, currentPrincipal.PrincipalId, timeProvider.GetUtcNow());

        if (outcome == ModerateOutcome.Terminal)
        {
            throw new ReviewTerminalStateException(request.ReviewId);
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);

        logger.LogInformation("Stage {Stage}: review {ReviewId} moderate({Action}) {Outcome}", "ReviewPersisted", review.ReviewId, action, outcome);

        await auditLogWriter.WriteAsync(
            AuditLogEntry.Create(
                "kart-review-service",
                currentPrincipal.PrincipalId,
                "moderator",
                $"ModerateReview:{action}",
                "Review",
                review.ReviewId.ToString(),
                request.Reason is null ? null : new Dictionary<string, object?> { ["reason"] = request.Reason }),
            cancellationToken);

        return ReviewResponse.FromDomain(review);
    }
}
