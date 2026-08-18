using Kart.Review.Domain.Idempotency;

namespace Kart.Review.Application.Common.Interfaces;

/// <summary>
/// design-decisions.md's Idempotency Mechanism decision: layer 1 of two independent layers (the
/// other being the DB-level <c>UNIQUE (order_id, sku)</c>/<c>UNIQUE (idempotency_key)</c>
/// constraints checked separately by each handler). Modeled directly on kart-payment-service's
/// <c>IIdempotencyGuard</c> (PAY-2) — reserve before doing any work, confirm once it resolves —
/// generalized here into a MediatR pipeline behaviour (<see cref="Behaviours.IdempotencyBehaviour{TRequest,TResponse}"/>)
/// since no service on the platform had done that generalization yet.
/// </summary>
public interface IIdempotencyGuard
{
    Task<IdempotencyReservation> ReserveOrReplayAsync(string idempotencyKey, IdempotencyEndpoint endpoint, string requestPayloadJson, string actingPrincipal, CancellationToken cancellationToken);

    Task ConfirmAsync(string idempotencyKey, IdempotencyEndpoint endpoint, string storedResponseJson, CancellationToken cancellationToken);
}

public enum IdempotencyOutcome
{
    /// <summary>No live record for this (key, endpoint) — proceed with a new attempt.</summary>
    New,

    /// <summary>Identical-payload replay within the 24h TTL — return <see cref="IdempotencyReservation.StoredResponseJson"/> with no re-execution.</summary>
    ReplayHit,

    /// <summary>Same key reused with a different request payload within the TTL — 422.</summary>
    Conflict,
}

public sealed record IdempotencyReservation(IdempotencyOutcome Outcome, string? StoredResponseJson);
