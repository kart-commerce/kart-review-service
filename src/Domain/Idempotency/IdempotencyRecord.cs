namespace Kart.Review.Domain.Idempotency;

/// <summary>
/// A separate, non-aggregate entity from <see cref="Reviews.Review"/> — design-decisions.md's
/// Idempotency Mechanism decision generalizes kart-payment-service's PAY-2 pattern (reserve →
/// caller does the work → confirm) here for a non-money-moving write. Keyed on the natural
/// composite <c>(IdempotencyKey, Endpoint)</c>, matching kart-payment-service's own
/// <c>IdempotencyRecord</c> precedent exactly.
/// </summary>
public sealed class IdempotencyRecord
{
    private static readonly TimeSpan ReplayWindow = TimeSpan.FromHours(24);

    public string IdempotencyKey { get; private set; } = string.Empty;

    public IdempotencyEndpoint Endpoint { get; private set; }

    public string RequestPayloadHash { get; private set; } = string.Empty;

    /// <summary>Set once the wrapped command resolves; null while the reserve-then-confirm cycle is still in flight.</summary>
    public string? StoredResponse { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset ExpiresAt { get; private set; }

    public DateTimeOffset UpdatedAt { get; private set; }

    public string CreatedBy { get; private set; } = string.Empty;

    public string UpdatedBy { get; private set; } = string.Empty;

    /// <summary>EF Core materialization only.</summary>
    private IdempotencyRecord()
    {
    }

    public static IdempotencyRecord Reserve(string idempotencyKey, IdempotencyEndpoint endpoint, string requestPayloadHash, string actingPrincipal, DateTimeOffset now) => new()
    {
        IdempotencyKey = idempotencyKey,
        Endpoint = endpoint,
        RequestPayloadHash = requestPayloadHash,
        CreatedAt = now,
        ExpiresAt = now.Add(ReplayWindow),
        UpdatedAt = now,
        CreatedBy = actingPrincipal,
        UpdatedBy = actingPrincipal,
    };

    public bool MatchesPayload(string requestPayloadHash) => RequestPayloadHash == requestPayloadHash;

    public bool IsLive(DateTimeOffset now) => now < ExpiresAt;

    /// <summary>Reuses this row as a brand-new logical attempt once its TTL has expired — an UPDATE in place, never a second INSERT (the primary key is `(IdempotencyKey, Endpoint)` alone).</summary>
    public void Reopen(string requestPayloadHash, string actingPrincipal, DateTimeOffset now)
    {
        RequestPayloadHash = requestPayloadHash;
        StoredResponse = null;
        CreatedAt = now;
        ExpiresAt = now.Add(ReplayWindow);
        UpdatedAt = now;
        CreatedBy = actingPrincipal;
        UpdatedBy = actingPrincipal;
    }

    public void Confirm(string storedResponseJson, DateTimeOffset now)
    {
        StoredResponse = storedResponseJson;
        UpdatedAt = now;
    }
}
