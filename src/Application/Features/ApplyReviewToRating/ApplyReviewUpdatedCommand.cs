using MediatR;

namespace Kart.Review.Application.Features.ApplyReviewToRating;

/// <summary>REV-6: internal self-consumption of this service's own <c>ReviewUpdated</c>.</summary>
public sealed record ApplyReviewUpdatedCommand(Guid OrderId, string Sku, int OldRating, int NewRating) : IRequest;
