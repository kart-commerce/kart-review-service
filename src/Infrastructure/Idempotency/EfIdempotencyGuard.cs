using Kart.Review.Application.Common.Interfaces;
using Kart.Review.Domain.Idempotency;
using Kart.Review.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Kart.Review.Infrastructure.Idempotency;

/// <summary>
/// The DB-backed implementation of <see cref="IIdempotencyGuard"/> — modeled directly on
/// kart-payment-service's <c>EfIdempotencyGuard</c> (PAY-2). <c>idempotency_keys</c>' own
/// <c>(idempotency_key, endpoint)</c> PRIMARY KEY is the actual race-closer across two concurrent
/// requests with the same key; the lookup-then-insert here is a TOCTOU race like any other, so a
/// losing concurrent request does NOT surface as an error — it waits briefly for the winner to
/// confirm and replays its result. This is exactly the "no double-processing under concurrent
/// duplicate requests" property the user asked for, applied here to a non-money-moving write the
/// same way kart-payment-service applies it to a money-moving one.
/// </summary>
public sealed class EfIdempotencyGuard(ReviewDbContext dbContext, TimeProvider timeProvider) : IIdempotencyGuard
{
    private const string PostgresUniqueViolationSqlState = "23505";
    private const int MaxAttempts = 40;
    private static readonly TimeSpan PollDelay = TimeSpan.FromMilliseconds(50);

    public async Task<IdempotencyReservation> ReserveOrReplayAsync(string idempotencyKey, IdempotencyEndpoint endpoint, string requestPayloadJson, string actingPrincipal, CancellationToken cancellationToken)
    {
        var requestPayloadHash = Hash(requestPayloadJson);

        for (var attempt = 0; attempt < MaxAttempts; attempt++)
        {
            var now = timeProvider.GetUtcNow();
            var existing = await dbContext.IdempotencyRecords
                .FirstOrDefaultAsync(r => r.IdempotencyKey == idempotencyKey && r.Endpoint == endpoint, cancellationToken);

            if (existing is not null && existing.IsLive(now))
            {
                if (!existing.MatchesPayload(requestPayloadHash))
                {
                    return new IdempotencyReservation(IdempotencyOutcome.Conflict, null);
                }

                if (existing.StoredResponse is not null)
                {
                    return new IdempotencyReservation(IdempotencyOutcome.ReplayHit, existing.StoredResponse);
                }

                // Reserved (by us on a prior loop iteration, or by a concurrent request) but not
                // yet confirmed — wait briefly and re-check rather than assuming it was abandoned.
                dbContext.ChangeTracker.Clear();
                await Task.Delay(PollDelay, cancellationToken);
                continue;
            }

            if (existing is not null)
            {
                // Expired past its 24h TTL — reused as a brand-new logical attempt, an UPDATE in
                // place, never a second INSERT (the primary key is (IdempotencyKey, Endpoint) alone).
                existing.Reopen(requestPayloadHash, actingPrincipal, now);
            }
            else
            {
                dbContext.IdempotencyRecords.Add(IdempotencyRecord.Reserve(idempotencyKey, endpoint, requestPayloadHash, actingPrincipal, now));
            }

            try
            {
                await dbContext.SaveChangesAsync(cancellationToken);
                return new IdempotencyReservation(IdempotencyOutcome.New, null);
            }
            catch (DbUpdateException ex) when (ex.InnerException is PostgresException { SqlState: PostgresUniqueViolationSqlState })
            {
                dbContext.ChangeTracker.Clear();
            }
        }

        throw new InvalidOperationException($"Could not resolve an idempotency reservation for '{idempotencyKey}'/{endpoint} after {MaxAttempts} attempts.");
    }

    public async Task ConfirmAsync(string idempotencyKey, IdempotencyEndpoint endpoint, string storedResponseJson, CancellationToken cancellationToken)
    {
        var record = await dbContext.IdempotencyRecords
            .FirstOrDefaultAsync(r => r.IdempotencyKey == idempotencyKey && r.Endpoint == endpoint, cancellationToken);

        record?.Confirm(storedResponseJson, timeProvider.GetUtcNow());
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private static string Hash(string requestPayloadJson)
    {
        var bytes = System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(requestPayloadJson));
        return Convert.ToHexString(bytes);
    }
}
