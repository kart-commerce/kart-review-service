using Kart.Review.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Kart.Review.Api.HealthChecks;

/// <summary>Readiness signal for `/health/ready` — a database that is reachable but behind on migrations must fail readiness too, so a pod never serves traffic against an unmigrated schema.</summary>
public sealed class ReviewDbHealthCheck(ReviewDbContext dbContext) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        try
        {
            var pending = (await dbContext.Database.GetPendingMigrationsAsync(cancellationToken)).ToArray();

            return pending.Length == 0
                ? HealthCheckResult.Healthy()
                : HealthCheckResult.Unhealthy($"{pending.Length} pending migration(s): {string.Join(", ", pending)}");
        }
        catch (Exception exception)
        {
            return HealthCheckResult.Unhealthy("Review database is unreachable", exception);
        }
    }
}
