using Kart.Review.Application.Common.Behaviours;
using Kart.Review.Application.Common.Models;
using Kart.Review.Domain.Idempotency;
using MediatR;

namespace Kart.Review.Application.Features.SubmitReview;

/// <summary>REV-3: api-contract.yaml's <c>POST /v1/reviews</c>. <c>UserId</c> is resolved from the JWT by the endpoint, never taken from the request body.</summary>
public sealed record SubmitReviewCommand(Guid OrderId, string Sku, int Rating, string BodyText, string IdempotencyKey) : IRequest<ReviewResponse>, IIdempotentCommand
{
    IdempotencyEndpoint IIdempotentCommand.Endpoint => IdempotencyEndpoint.SubmitReview;
}
