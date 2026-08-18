using Kart.Review.Application.Common.Exceptions;
using Kart.Review.Application.Common.Interfaces;
using Kart.Review.Application.Common.Models;
using Kart.Review.Domain.Reviews;
using Kart.Shared.Auditing;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Kart.Review.Application.Features.EditReview;

/// <summary>REV-9: re-runs the classifier against the new content and branches per ddd-model.md's Defer-until-outcome invariant, generalized to edits (see <see cref="Review.Edit"/>).</summary>
public sealed class EditReviewCommandHandler(
    IReviewRepository reviews,
    IUnitOfWork unitOfWork,
    IContentSafetyClassifier classifier,
    ICurrentPrincipal currentPrincipal,
    IAuditLogWriter auditLogWriter,
    TimeProvider timeProvider,
    ILogger<EditReviewCommandHandler> logger) : IRequestHandler<EditReviewCommand, ReviewResponse>
{
    public async Task<ReviewResponse> Handle(EditReviewCommand request, CancellationToken cancellationToken)
    {
        logger.LogInformation("Stage {Stage}: EditReview received for review {ReviewId}", "EditReviewRequestReceived", request.ReviewId);

        var review = await reviews.GetByIdAsync(request.ReviewId, cancellationToken) ?? throw new ReviewNotFoundException(request.ReviewId);

        if (!review.IsAuthor(currentPrincipal.UserId))
        {
            throw new NotReviewAuthorException(request.ReviewId);
        }

        var newRating = request.Rating.HasValue ? Rating.From(request.Rating.Value) : (Rating?)null;

        // Classify whatever the final content will actually be — merged against the currently
        // staged PendingRevision if one exists, else the currently-Published/queued content (same
        // baseline Review.Edit itself merges against; see its own remarks on this judgment call).
        var textToClassify = request.BodyText ?? review.PendingRevision?.NewBodyText ?? review.BodyText;
        var classification = await classifier.ClassifyAsync(textToClassify, cancellationToken);
        var cleared = classification == ContentSafetyDecision.Cleared;

        var outcome = review.Edit(newRating, request.BodyText, cleared, currentPrincipal.PrincipalId, timeProvider.GetUtcNow());

        switch (outcome)
        {
            case EditOutcome.WindowClosed:
                throw new EditWindowClosedException();
            case EditOutcome.Terminal:
                throw new ReviewTerminalStateException(request.ReviewId);
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);

        logger.LogInformation("Stage {Stage}: review {ReviewId} edit {Outcome}", "ReviewPersisted", review.ReviewId, outcome);

        await auditLogWriter.WriteAsync(
            AuditLogEntry.Create("kart-review-service", currentPrincipal.PrincipalId, "user", "EditReview", "Review", review.ReviewId.ToString()),
            cancellationToken);

        return ReviewResponse.FromDomain(review);
    }
}
