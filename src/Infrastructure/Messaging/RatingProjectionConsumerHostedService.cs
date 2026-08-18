using System.Text;
using System.Text.Json;
using Kart.Review.Application.Features.ApplyReviewToRating;
using Kart.Shared.Messaging;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using RabbitMQ.Client;

namespace Kart.Review.Infrastructure.Messaging;

/// <summary>
/// REV-6: genuine RabbitMQ self-consumption of this service's own <c>review.exchange</c> — bound
/// to all three published routing keys via <c>review.rating-projection.queue</c> (tickets.md's
/// dependency chain has REV-6 depend on REV-4, the relay, confirming this is meant as a real
/// self-consumer rather than an internal-poller shortcut like REV-5). Discriminates which of the
/// three events arrived via the embedded <c>eventType</c> field (see <c>EventPayloads.cs</c>'s own
/// remarks — the shared <c>RabbitMqConsumerHostedServiceBase</c> does not expose the AMQP routing
/// key to <c>ProcessAsync</c>, so a single queue bound to multiple routing keys needs some
/// in-payload discriminator).
/// </summary>
public sealed class RatingProjectionConsumerHostedService(
    IConnectionFactory connectionFactory,
    MessageBusManifest manifest,
    IServiceScopeFactory scopeFactory,
    ILogger<RatingProjectionConsumerHostedService> logger)
    : RabbitMqConsumerHostedServiceBase(connectionFactory, manifest, scopeFactory, logger, "x-review-rating-projection-retry-count")
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    protected override string QueueName => "review.rating-projection.queue";

    protected override async Task ProcessAsync(ReadOnlyMemory<byte> body, IServiceProvider scopedProvider, CancellationToken cancellationToken)
    {
        var json = Encoding.UTF8.GetString(body.Span);
        using var document = JsonDocument.Parse(json);
        var eventType = document.RootElement.GetProperty("eventType").GetString();

        var mediator = scopedProvider.GetRequiredService<IMediator>();

        switch (eventType)
        {
            case "ReviewSubmitted":
                var submitted = JsonSerializer.Deserialize<ReviewSubmittedEnvelope>(json, JsonOptions)!;
                logger.LogInformation("Stage {Stage}: ReviewSubmitted consumed from {Queue} for review {ReviewId}", "EventConsumed", QueueName, submitted.ReviewId);
                await mediator.Send(new ApplyReviewSubmittedCommand(submitted.OrderId, submitted.Sku, submitted.Rating, submitted.ReviewId, submitted.UserId), cancellationToken);
                break;

            case "ReviewUpdated":
                var updated = JsonSerializer.Deserialize<ReviewUpdatedEnvelope>(json, JsonOptions)!;
                logger.LogInformation("Stage {Stage}: ReviewUpdated consumed from {Queue} for order {OrderId} sku {Sku}", "EventConsumed", QueueName, updated.OrderId, updated.Sku);
                await mediator.Send(new ApplyReviewUpdatedCommand(updated.OrderId, updated.Sku, updated.OldRating, updated.NewRating), cancellationToken);
                break;

            case "ReviewUnpublished":
                var unpublished = JsonSerializer.Deserialize<ReviewUnpublishedEnvelope>(json, JsonOptions)!;
                logger.LogInformation("Stage {Stage}: ReviewUnpublished consumed from {Queue} for review {ReviewId}", "EventConsumed", QueueName, unpublished.ReviewId);
                await mediator.Send(new ApplyReviewUnpublishedCommand(unpublished.OrderId, unpublished.Sku, unpublished.Rating, unpublished.ReviewId, unpublished.UserId, unpublished.Reason), cancellationToken);
                break;

            default:
                throw new InvalidOperationException($"review.rating-projection.queue received an unrecognized eventType '{eventType}'.");
        }
    }

    private sealed record ReviewSubmittedEnvelope(Guid OrderId, string Sku, int Rating, Guid ReviewId, Guid UserId);

    private sealed record ReviewUpdatedEnvelope(Guid OrderId, string Sku, int OldRating, int NewRating);

    private sealed record ReviewUnpublishedEnvelope(Guid OrderId, string Sku, int Rating, Guid ReviewId, Guid UserId, string Reason);
}
