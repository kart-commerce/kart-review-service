namespace Kart.Review.Application.Common.Exceptions;

/// <summary>The caller is authenticated but is not this review's author — 403 (BRD §24.1.2's ownership-gated CanWrite/CanDelete rule).</summary>
public sealed class NotReviewAuthorException(Guid reviewId) : Exception($"Caller is not the author of review {reviewId}.");
