using MediatR;

namespace Kart.Review.Application.Features.ApplyReviewToRating;

/// <summary>REV-6: internal self-consumption of this service's own <c>ReviewUnpublished</c>.</summary>
public sealed record ApplyReviewUnpublishedCommand(Guid OrderId, string Sku, int Rating, Guid ReviewId, Guid UserId, string Reason) : IRequest;
