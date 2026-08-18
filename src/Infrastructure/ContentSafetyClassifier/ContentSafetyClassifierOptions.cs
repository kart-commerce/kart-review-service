namespace Kart.Review.Infrastructure.ContentSafetyClassifier;

/// <summary>Binds the `"ContentSafetyClassifier"` config section.</summary>
public sealed class ContentSafetyClassifierOptions
{
    public const string SectionName = "ContentSafetyClassifier";

    /// <summary>architecture.md's ≤120ms sub-budget within the overall P95 &lt; 300ms write-path target.</summary>
    public int TimeoutMilliseconds { get; set; } = 120;

    public int CircuitBreakerFailureThreshold { get; set; } = 5;

    public int CircuitBreakerBreakDurationSeconds { get; set; } = 30;

    /// <summary>
    /// Case-insensitive substrings that flag content for the human moderation queue — a
    /// deterministic stand-in for a real ML content-safety service (none exists anywhere on the
    /// platform yet; this is a documented, pluggable seam per <c>IContentSafetyClassifier</c>'s
    /// own remarks).
    /// </summary>
    public string[] BannedTerms { get; set; } = ["scam", "fraud", "fake product", "counterfeit"];

    /// <summary>
    /// Test-only hook: an artificial delay injected before every classification, so integration
    /// tests can exercise the timeout → fail-safe-to-queue path deterministically without a flaky
    /// real-latency race. Zero (the default) in every real environment.
    /// </summary>
    public int SimulatedLatencyMilliseconds { get; set; }
}
