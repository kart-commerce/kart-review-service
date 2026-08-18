using MediatR;

namespace Kart.Review.Application.Features.ApplyReviewToRating;

/// <summary>REV-6: internal self-consumption of this service's own <c>ReviewSubmitted</c> (via <c>review.rating-projection.queue</c>) to keep the canonical <c>ProductRating</c> aggregate in sync (ADR-0014).</summary>
public sealed record ApplyReviewSubmittedCommand(Guid OrderId, string Sku, int Rating, Guid ReviewId, Guid UserId) : IRequest;
