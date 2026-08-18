using MediatR;

namespace Kart.Review.Application.Features.ConsumeOrderDelivered;

/// <summary>REV-2: consumes kart-order-service's <c>OrderDelivered</c> — upserts <c>VerifiedPurchaseRecord.deliveredAt</c>, keyed on <c>orderId</c> (ADR-0005). Feeds <c>POST /v1/reviews</c>'s hard eligibility gate.</summary>
public sealed record ConsumeOrderDeliveredCommand(Guid OrderId, DateTimeOffset DeliveredAt) : IRequest;
