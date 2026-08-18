namespace Kart.Review.Application.Common.Interfaces;

/// <summary>
/// REV-1: the synchronous automated content-safety pre-check every submission/edit runs through
/// (architecture.md's ≤120ms sub-budget within the overall P95 &lt; 300ms write-path target). No
/// such external service exists anywhere on the platform yet — this is a pluggable seam; the
/// default implementation (<c>Infrastructure/ContentSafetyClassifier/RuleBasedContentSafetyClassifier</c>)
/// is a deterministic rule-based stand-in wrapped in a circuit breaker + timeout policy, swappable
/// for a real ML service later without touching any caller.
/// </summary>
public interface IContentSafetyClassifier
{
    Task<ContentSafetyDecision> ClassifyAsync(string bodyText, CancellationToken cancellationToken);
}

/// <summary>
/// Fail-safe, never fail-open (design-decisions.md's Resilience Pattern decision): a timeout or
/// open circuit maps to <see cref="FailSafe"/>, which every caller treats identically to
/// <see cref="Flagged"/> — routed to the human moderation queue, never auto-published.
/// </summary>
public enum ContentSafetyDecision
{
    Cleared,
    Flagged,
    FailSafe,
}
