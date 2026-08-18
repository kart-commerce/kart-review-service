using Kart.Review.Application.Common.Behaviours;
using Kart.Review.Application.Common.Models;
using Kart.Review.Domain.Idempotency;
using MediatR;

namespace Kart.Review.Application.Features.EditReview;

/// <summary>REV-9: api-contract.yaml's <c>PATCH /v1/reviews/{id}</c>. Author-only, 30-day edit window (ddd-model.md).</summary>
public sealed record EditReviewCommand(Guid ReviewId, int? Rating, string? BodyText, string IdempotencyKey) : IRequest<ReviewResponse>, IIdempotentCommand
{
    IdempotencyEndpoint IIdempotentCommand.Endpoint => IdempotencyEndpoint.EditReview;
}
