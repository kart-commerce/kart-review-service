namespace Kart.Review.Domain.Idempotency;

/// <summary>The two non-retry-safe writes api-contract.yaml mandates an <c>Idempotency-Key</c> header on. The <c>(idempotency_key, endpoint)</c> composite scope prevents a submit-key and an edit-key from colliding.</summary>
public enum IdempotencyEndpoint
{
    SubmitReview,
    EditReview,
}
