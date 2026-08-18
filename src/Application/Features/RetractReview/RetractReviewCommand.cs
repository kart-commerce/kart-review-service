using Kart.Review.Application.Common.Models;
using MediatR;

namespace Kart.Review.Application.Features.RetractReview;

/// <summary>REV-10: api-contract.yaml's <c>DELETE /v1/reviews/{id}</c>. No Idempotency-Key — retraction is itself idempotent (safe to retry).</summary>
public sealed record RetractReviewCommand(Guid ReviewId) : IRequest<ReviewResponse>;
