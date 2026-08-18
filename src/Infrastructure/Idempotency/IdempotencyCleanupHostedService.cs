using Kart.Review.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Kart.Review.Infrastructure.Idempotency;

/// <summary>
/// TTL cleanup for `idempotency_keys` — mirrors kart-payment-service's
/// <c>IdempotencyPartitionMaintenanceHostedService</c>. A single, non-partitioned table (see
/// <c>Persistence/Configurations/IdempotencyRecordConfiguration.cs</c>'s own remarks for why
/// partitioning was rejected there — the same reasoning applies here); TTL cleanup is a batched
/// <c>DELETE</c>, not a partition-drop. A generous safety margin (2 days past <c>expires_at</c>)
/// keeps this from competing with write-path traffic near the boundary.
/// </summary>
public sealed class IdempotencyCleanupHostedService(
    IServiceScopeFactory scopeFactory,
    TimeProvider timeProvider,
    ILogger<IdempotencyCleanupHostedService> logger) : BackgroundService
{
    private static readonly TimeSpan TickInterval = TimeSpan.FromHours(1);
    private static readonly TimeSpan RetentionPastExpiry = TimeSpan.FromDays(2);
    private const int BatchSize = 1000;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await RunOnceAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Idempotency-key TTL cleanup run failed; will retry on the next tick.");
            }

            await Task.Delay(TickInterval, stoppingToken);
        }
    }

    public async Task RunOnceAsync(CancellationToken cancellationToken)
    {
        using var scope = scopeFactory.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ReviewDbContext>();

        var cutoff = timeProvider.GetUtcNow().Subtract(RetentionPastExpiry);
        int deletedThisRun;
        var totalDeleted = 0;

        do
        {
            var staleKeys = await dbContext.IdempotencyRecords
                .Where(r => r.ExpiresAt < cutoff)
                .OrderBy(r => r.ExpiresAt)
                .Take(BatchSize)
                .ToListAsync(cancellationToken);

            deletedThisRun = staleKeys.Count;
            if (deletedThisRun > 0)
            {
                dbContext.IdempotencyRecords.RemoveRange(staleKeys);
                await dbContext.SaveChangesAsync(cancellationToken);
                totalDeleted += deletedThisRun;
            }
        }
        while (deletedThisRun == BatchSize && !cancellationToken.IsCancellationRequested);

        if (totalDeleted > 0)
        {
            logger.LogInformation("Idempotency-key TTL cleanup removed {Count} row(s) expired before {Cutoff}.", totalDeleted, cutoff);
        }
    }
}
