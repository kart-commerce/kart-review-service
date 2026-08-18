namespace Kart.Review.Application.Common.Exceptions;

/// <summary>The same <c>Idempotency-Key</c> was reused with a materially different request body — 422 (design-decisions.md's Idempotency Mechanism decision).</summary>
public sealed class IdempotencyConflictException(string idempotencyKey) : Exception($"Idempotency-Key '{idempotencyKey}' was reused with a different request body.");
