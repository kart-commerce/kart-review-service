using System.Text;
using System.Text.Json;
using Kart.Review.Application.Features.ConsumeOrderCreated;
using Kart.Review.Application.Features.ConsumeOrderDelivered;
using Kart.Shared.Messaging;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using RabbitMQ.Client;

namespace Kart.Review.Infrastructure.Messaging;

/// <summary>
/// REV-2: consumes kart-order-service's <c>OrderCreated</c>/<c>OrderDelivered</c> via
/// <c>review.order-events.queue</c> (bound to the external <c>order.exchange</c> on both routing
/// keys). Order-service's own payloads carry no explicit type discriminator, so this
/// distinguishes them structurally: <c>OrderDelivered</c>'s payload is <c>{orderId,
/// deliveredAt}</c> only; <c>OrderCreated</c>'s is <c>{orderId, userId, items, total}</c> — the
/// presence of <c>deliveredAt</c> vs. <c>items</c> is unambiguous between the two.
/// </summary>
public sealed class OrderEventsConsumerHostedService(
    IConnectionFactory connectionFactory,
    MessageBusManifest manifest,
    IServiceScopeFactory scopeFactory,
    ILogger<OrderEventsConsumerHostedService> logger)
    : RabbitMqConsumerHostedServiceBase(connectionFactory, manifest, scopeFactory, logger, "x-review-order-events-retry-count")
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    protected override string QueueName => "review.order-events.queue";

    protected override async Task ProcessAsync(ReadOnlyMemory<byte> body, IServiceProvider scopedProvider, CancellationToken cancellationToken)
    {
        var json = Encoding.UTF8.GetString(body.Span);
        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;

        var mediator = scopedProvider.GetRequiredService<IMediator>();

        if (root.TryGetProperty("deliveredAt", out var deliveredAtElement) && !root.TryGetProperty("items", out _))
        {
            var orderId = root.GetProperty("orderId").GetGuid();
            var deliveredAt = deliveredAtElement.GetDateTimeOffset();
            logger.LogInformation("Stage {Stage}: OrderDelivered consumed from {Queue} for order {OrderId}", "EventConsumed", QueueName, orderId);
            await mediator.Send(new ConsumeOrderDeliveredCommand(orderId, deliveredAt), cancellationToken);
            return;
        }

        if (root.TryGetProperty("items", out _))
        {
            var payload = JsonSerializer.Deserialize<OrderCreatedEnvelope>(json, JsonOptions)!;
            var skus = payload.Items.Select(i => i.Sku).ToList();
            logger.LogInformation("Stage {Stage}: OrderCreated consumed from {Queue} for order {OrderId}", "EventConsumed", QueueName, payload.OrderId);
            await mediator.Send(new ConsumeOrderCreatedCommand(payload.OrderId, payload.UserId, skus), cancellationToken);
            return;
        }

        throw new InvalidOperationException("review.order-events.queue received a message matching neither OrderCreated nor OrderDelivered's expected shape.");
    }

    private sealed record OrderCreatedEnvelope(Guid OrderId, Guid UserId, IReadOnlyList<OrderLineItemEnvelope> Items);

    private sealed record OrderLineItemEnvelope(string Sku, int Qty, decimal UnitPrice);
}
