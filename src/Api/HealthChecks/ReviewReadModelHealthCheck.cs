using Kart.Review.Infrastructure.Persistence.ReadModel;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Kart.Review.Api.HealthChecks;

/// <summary>Readiness signal for the Mongo read side — `GET /v1/reviews` (REV-8) is unusable if this is unreachable even though PostgreSQL itself is healthy.</summary>
public sealed class ReviewReadModelHealthCheck(ReviewReadDbContext readDbContext) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        try
        {
            await readDbContext.Reviews.EstimatedDocumentCountAsync(cancellationToken: cancellationToken);
            return HealthCheckResult.Healthy();
        }
        catch (Exception exception)
        {
            return HealthCheckResult.Unhealthy("Review read-model MongoDB is unreachable", exception);
        }
    }
}
