using Kart.Review.Application.Common.Models;
using Kart.Review.Domain.Reviews;
using Kart.Review.Infrastructure.Persistence;
using Kart.Review.Infrastructure.Persistence.ReadModel;
using Kart.Shared.Observability;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Kart.Review.Infrastructure.Messaging;

/// <summary>
/// REV-5: a direct-Postgres poller over `review_outbox` (`read_model_projected_at IS NULL` —
/// `idx_review_outbox_unprojected`), independent of the RabbitMQ relay's own `published_at`
/// marker — mirrors kart-order-service's/kart-user-service's own Mongo projector precedent
/// (deliberately not a RabbitMQ self-consumer for this one; see `ReviewOutboxEvent`'s remarks).
///
/// Always rebuilds the read-model document from CURRENT PostgreSQL state, never from the outbox
/// row's own stored payload — "the read model must be rebuildable from the write model"
/// (PLATFORM_BLUEPRINT.md's CQRS standard) — which also makes this self-correcting against rapid
/// out-of-order transitions (e.g. a review retracted moments after being published: by the time
/// this poller catches up to the ReviewSubmitted row, the live Review is already Retracted, so it
/// deletes rather than inserts).
/// </summary>
public sealed class ReviewReadModelProjectionHostedService(IServiceScopeFactory scopeFactory, ILogger<ReviewReadModelProjectionHostedService> logger) : BackgroundService
{
    private const string FlowName = "ReviewModerationAndRatings";
    private static readonly TimeSpan PollInterval = TimeSpan.FromSeconds(2);
    private const int BatchSize = 100;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ProjectPendingBatchAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Review read-model projector failed to process a batch; retrying after the normal poll interval.");
            }

            await Task.Delay(PollInterval, stoppingToken);
        }
    }

    private async Task ProjectPendingBatchAsync(CancellationToken cancellationToken)
    {
        using var scope = scopeFactory.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ReviewDbContext>();
        var readModel = scope.ServiceProvider.GetRequiredService<Application.Common.Interfaces.IReviewReadModelRepository>();
        var displayNameResolver = scope.ServiceProvider.GetRequiredService<IUserDisplayNameResolver>();

        var pending = await dbContext.ReviewOutboxEvents
            .Where(e => e.ReadModelProjectedAt == null)
            .OrderBy(e => e.OccurredAt)
            .Take(BatchSize)
            .ToListAsync(cancellationToken);

        if (pending.Count == 0)
        {
            return;
        }

        using var _ = KartFlowContext.Push(FlowName);
        var now = DateTimeOffset.UtcNow;

        foreach (var outboxEvent in pending)
        {
            if (outboxEvent.EventType == "ReviewUnpublished")
            {
                await readModel.DeleteAsync(outboxEvent.AggregateId, cancellationToken);
            }
            else
            {
                var review = await dbContext.Reviews.FirstOrDefaultAsync(r => r.Id == outboxEvent.AggregateId, cancellationToken);
                if (review is null || review.Status != ModerationStatus.Published)
                {
                    // Rebuilt from current truth: no longer (or never) public by the time this
                    // poller caught up — nothing to project, and any stale doc is removed.
                    await readModel.DeleteAsync(outboxEvent.AggregateId, cancellationToken);
                }
                else
                {
                    var displayName = await displayNameResolver.ResolveAsync(review.UserId, cancellationToken);
                    var publicResponse = new PublicReviewResponse(
                        review.ReviewId.Value, review.OrderId.Value, review.Sku.Value, displayName,
                        review.Rating.Value, review.BodyText, review.FirstPublishedAt!.Value, review.LastEditedAt);
                    await readModel.UpsertAsync(publicResponse, cancellationToken);
                }
            }

            outboxEvent.MarkReadModelProjected(now);

            logger.LogInformation(
                "Stage {Stage}: read model persisted for review {ReviewId} (event {EventType})",
                "ReadModelPersisted", outboxEvent.AggregateId, outboxEvent.EventType);
        }

        await dbContext.SaveChangesAsync(cancellationToken);
    }
}
