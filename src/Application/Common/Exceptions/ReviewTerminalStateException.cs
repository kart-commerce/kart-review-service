namespace Kart.Review.Application.Common.Exceptions;

/// <summary>The review is <c>Retracted</c> — terminal, no edit or moderator action is legal against it (ddd-model.md invariant #8).</summary>
public sealed class ReviewTerminalStateException(Guid reviewId) : Exception($"Review {reviewId} is in a terminal state (Retracted) and cannot be changed.");
