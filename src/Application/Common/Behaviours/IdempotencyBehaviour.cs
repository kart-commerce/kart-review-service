using System.Text.Json;
using Kart.Review.Application.Common.Exceptions;
using Kart.Review.Application.Common.Interfaces;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Kart.Review.Application.Common.Behaviours;

/// <summary>
/// Generalizes kart-payment-service's PAY-2 <c>IIdempotencyGuard</c> (reserve → do the work →
/// confirm) into a MediatR pipeline behaviour — no service on the platform had made that
/// generalization yet (confirmed via a repo-wide search across every kart-*-service). Any command
/// implementing <see cref="IIdempotentCommand"/> gets this for free just by being sent through
/// MediatR; <c>SubmitReviewCommand</c>/<c>EditReviewCommand</c> are the two opt-ins today
/// (api-contract.yaml's mandatory <c>Idempotency-Key</c> header on both).
///
/// This is layer 1 of design-decisions.md's two-layer idempotency mechanism — layer 2 (the DB
/// <c>UNIQUE (order_id, sku)</c> constraint) is checked independently inside the wrapped handler
/// itself, catching a duplicate that arrives with no matching key at all.
/// </summary>
public sealed class IdempotencyBehaviour<TRequest, TResponse>(
    IIdempotencyGuard guard,
    ICurrentPrincipal currentPrincipal,
    ILogger<IdempotencyBehaviour<TRequest, TResponse>> logger) : IPipelineBehavior<TRequest, TResponse>
    where TRequest : IIdempotentCommand
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);

    public async Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken cancellationToken)
    {
        var requestPayloadJson = JsonSerializer.Serialize(request, SerializerOptions);

        var reservation = await guard.ReserveOrReplayAsync(
            request.IdempotencyKey,
            request.Endpoint,
            requestPayloadJson,
            currentPrincipal.PrincipalId,
            cancellationToken);

        switch (reservation.Outcome)
        {
            case IdempotencyOutcome.Conflict:
                logger.LogWarning("Stage {Stage}: Idempotency-Key {IdempotencyKey} reused with a different body for {RequestName}", "IdempotencyConflict", request.IdempotencyKey, typeof(TRequest).Name);
                throw new IdempotencyConflictException(request.IdempotencyKey);

            case IdempotencyOutcome.ReplayHit:
                logger.LogInformation("Stage {Stage}: Idempotency-Key {IdempotencyKey} replayed for {RequestName}, no re-execution", "IdempotencyReplay", request.IdempotencyKey, typeof(TRequest).Name);
                return JsonSerializer.Deserialize<TResponse>(reservation.StoredResponseJson!, SerializerOptions)!;

            default:
                var response = await next();
                await guard.ConfirmAsync(request.IdempotencyKey, request.Endpoint, JsonSerializer.Serialize(response, SerializerOptions), cancellationToken);
                return response;
        }
    }
}
