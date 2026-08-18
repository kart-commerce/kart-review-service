namespace Kart.Review.Application.Common.Exceptions;

public sealed class ReviewNotFoundException(Guid reviewId) : Exception($"Review {reviewId} was not found.");
