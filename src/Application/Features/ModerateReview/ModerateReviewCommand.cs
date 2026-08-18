using Kart.Review.Application.Common.Models;
using MediatR;

namespace Kart.Review.Application.Features.ModerateReview;

/// <summary>REV-11: api-contract.yaml's <c>PATCH /v1/reviews/{id}/moderate</c>. Role gate (Support Agent/Admin) enforced at the endpoint via an authorization policy, not here.</summary>
public sealed record ModerateReviewCommand(Guid ReviewId, string Action, string? Reason) : IRequest<ReviewResponse>;
