using Kart.Review.Application.Common.Interfaces;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Polly;
using Polly.CircuitBreaker;
using Polly.Timeout;

namespace Kart.Review.Infrastructure.ContentSafetyClassifier;

/// <summary>
/// REV-1: the synchronous content-safety pre-check, wrapped in a timeout + circuit breaker that
/// is fail-safe (never fail-open) on either tripping — design-decisions.md's Resilience Pattern
/// decision: "fail-safe to human queue on timeout/open-circuit, never fail-open," because
/// fail-open would auto-publish unscreened content exactly when screening matters most. No real
/// content-safety service exists anywhere on the platform yet (confirmed by a repo-wide search) —
/// this deterministic rule-based check is a documented, pluggable stand-in
/// (<see cref="IContentSafetyClassifier"/>'s own remarks) behind the exact resilience shape a real
/// one would need.
/// </summary>
public sealed class RuleBasedContentSafetyClassifier : IContentSafetyClassifier
{
    private readonly ContentSafetyClassifierOptions _options;
    private readonly ILogger<RuleBasedContentSafetyClassifier> _logger;
    private readonly IAsyncPolicy _resiliencePolicy;

    public RuleBasedContentSafetyClassifier(IOptions<ContentSafetyClassifierOptions> options, ILogger<RuleBasedContentSafetyClassifier> logger)
    {
        _options = options.Value;
        _logger = logger;

        var timeoutPolicy = Policy.TimeoutAsync(TimeSpan.FromMilliseconds(_options.TimeoutMilliseconds), TimeoutStrategy.Optimistic);
        var circuitBreakerPolicy = Policy
            .Handle<Exception>()
            .CircuitBreakerAsync(
                _options.CircuitBreakerFailureThreshold,
                TimeSpan.FromSeconds(_options.CircuitBreakerBreakDurationSeconds),
                onBreak: (ex, duration) => _logger.LogWarning(ex, "Content-safety classifier circuit opened for {Duration}", duration),
                onReset: () => _logger.LogInformation("Content-safety classifier circuit reset"));

        // Circuit breaker outermost: a request that times out still counts as a failure toward
        // tripping the breaker, so sustained timeouts open the circuit exactly like a sustained
        // hard failure would.
        _resiliencePolicy = Policy.WrapAsync(circuitBreakerPolicy, timeoutPolicy);
    }

    public async Task<ContentSafetyDecision> ClassifyAsync(string bodyText, CancellationToken cancellationToken)
    {
        try
        {
            return await _resiliencePolicy.ExecuteAsync(
                async ct =>
                {
                    if (_options.SimulatedLatencyMilliseconds > 0)
                    {
                        await Task.Delay(_options.SimulatedLatencyMilliseconds, ct);
                    }

                    var flagged = _options.BannedTerms.Any(term => bodyText.Contains(term, StringComparison.OrdinalIgnoreCase));
                    return flagged ? ContentSafetyDecision.Flagged : ContentSafetyDecision.Cleared;
                },
                cancellationToken);
        }
        catch (TimeoutRejectedException)
        {
            _logger.LogWarning("Content-safety classifier timed out after {TimeoutMs}ms — fail-safe to human moderation queue", _options.TimeoutMilliseconds);
            return ContentSafetyDecision.FailSafe;
        }
        catch (BrokenCircuitException)
        {
            _logger.LogWarning("Content-safety classifier circuit is open — fail-safe to human moderation queue");
            return ContentSafetyDecision.FailSafe;
        }
    }
}
