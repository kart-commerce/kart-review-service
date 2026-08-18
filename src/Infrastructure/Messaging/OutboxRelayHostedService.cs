using System.Text;
using Kart.Review.Infrastructure.Persistence;
using Kart.Shared.Messaging;
using Kart.Shared.Observability;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using RabbitMQ.Client;

namespace Kart.Review.Infrastructure.Messaging;

/// <summary>
/// REV-4: relays `review_outbox` rows (`published_at IS NULL` — `idx_review_outbox_unpublished`)
/// to whichever exchange/routing key `contracts/message-bus-manifest.json`'s `publishedEvents`
/// maps each event type to. Declares the full manifest topology idempotently on every
/// (re)connect. Retries indefinitely until RabbitMQ is reachable rather than dead-lettering — the
/// publish-side half of at-least-once delivery. Mirrors kart-order-service's identically-shaped
/// relay.
/// </summary>
public sealed class OutboxRelayHostedService(
    IServiceScopeFactory scopeFactory,
    IConnectionFactory connectionFactory,
    MessageBusManifest manifest,
    ILogger<OutboxRelayHostedService> logger) : BackgroundService
{
    private const string FlowName = "ReviewModerationAndRatings";
    private static readonly TimeSpan PollInterval = TimeSpan.FromSeconds(5);
    private static readonly TimeSpan ReconnectDelay = TimeSpan.FromSeconds(10);
    private const int BatchSize = 100;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var connection = connectionFactory.CreateConnection();
                using var channel = connection.CreateModel();
                RabbitMqTopologyProvisioner.Declare(channel, manifest);

                await RunRelayLoopAsync(channel, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Review outbox relay lost its RabbitMQ connection; reconnecting in {Delay}.", ReconnectDelay);
                await Task.Delay(ReconnectDelay, stoppingToken);
            }
        }
    }

    private async Task RunRelayLoopAsync(IModel channel, CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            await RelayPendingBatchAsync(channel, stoppingToken);
            await Task.Delay(PollInterval, stoppingToken);
        }
    }

    private async Task RelayPendingBatchAsync(IModel channel, CancellationToken cancellationToken)
    {
        using var scope = scopeFactory.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ReviewDbContext>();

        var pending = await dbContext.ReviewOutboxEvents
            .Where(e => e.PublishedAt == null)
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
            var properties = channel.CreateBasicProperties();
            properties.Persistent = true;
            properties.MessageId = outboxEvent.Id.ToString();
            properties.ContentType = "application/json";

            var exchange = manifest.ExchangeFor(outboxEvent.EventType);
            var routingKey = manifest.RoutingKeyFor(outboxEvent.EventType);

            using var activity = RabbitMqTraceContext.StartPublishActivityFromStoredTraceParent(exchange, routingKey, outboxEvent.TraceParent, properties);

            channel.BasicPublish(
                exchange: exchange,
                routingKey: routingKey,
                basicProperties: properties,
                body: Encoding.UTF8.GetBytes(outboxEvent.Payload));

            outboxEvent.MarkPublished(now);

            logger.LogInformation(
                "Stage {Stage}: {EventType} outbox event {OutboxId} published to {Exchange}/{RoutingKey}",
                "OutboxEventPublished", outboxEvent.EventType, outboxEvent.Id, exchange, routingKey);
        }

        await dbContext.SaveChangesAsync(cancellationToken);
    }
}
