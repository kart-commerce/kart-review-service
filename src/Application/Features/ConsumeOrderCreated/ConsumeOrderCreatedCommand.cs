using MediatR;

namespace Kart.Review.Application.Features.ConsumeOrderCreated;

/// <summary>REV-2: consumes kart-order-service's <c>OrderCreated</c> (ADR-0021) — upserts <c>VerifiedPurchaseRecord.userId</c>/<c>skus</c>, keyed on <c>orderId</c>. Dispatched by the RabbitMQ consumer hosted service; <see cref="IRequest"/> (no response) since this is a pure fire-and-forget projection.</summary>
public sealed record ConsumeOrderCreatedCommand(Guid OrderId, Guid UserId, IReadOnlyList<string> Skus) : IRequest;
