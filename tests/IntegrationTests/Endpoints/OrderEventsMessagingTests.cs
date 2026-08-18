using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using FluentAssertions;
using Kart.Review.IntegrationTests.Infrastructure;
using RabbitMQ.Client;
using Xunit;

namespace Kart.Review.IntegrationTests.Endpoints;

/// <summary>
/// REV-2 end-to-end: publishes REAL messages onto the REAL RabbitMQ broker's `order.exchange`
/// (simulating kart-order-service, which this test suite has no live instance of), and asserts
/// this service's own `OrderEventsConsumerHostedService` actually consumes them and makes
/// `POST /v1/reviews`'s eligibility gate pass — proving the message-bus wiring end to end, not
/// just the HTTP surface.
/// </summary>
[Collection(ReviewApiCollection.Name)]
public sealed class OrderEventsMessagingTests(ReviewApiFactory factory)
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    [Fact]
    public async Task PublishingOrderCreatedThenOrderDelivered_EventuallyAllowsReviewSubmission()
    {
        var factoryConn = new ConnectionFactory
        {
            HostName = factory.RabbitMqHostName,
            Port = factory.RabbitMqPort,
            UserName = ReviewApiFactory.RabbitMqUserName,
            Password = ReviewApiFactory.RabbitMqPassword,
        };

        using var connection = factoryConn.CreateConnection();
        using var channel = connection.CreateModel();
        channel.ExchangeDeclare("order.exchange", "topic", durable: true);

        var orderId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        const string sku = "SKU-MSG-1";

        var orderCreatedPayload = JsonSerializer.SerializeToUtf8Bytes(new
        {
            orderId,
            userId,
            items = new[] { new { sku, qty = 1, unitPrice = 9.99m } },
            total = 9.99m,
        }, JsonOptions);
        channel.BasicPublish("order.exchange", "order.order.created", body: orderCreatedPayload);

        var orderDeliveredPayload = JsonSerializer.SerializeToUtf8Bytes(new { orderId, deliveredAt = DateTimeOffset.UtcNow }, JsonOptions);
        channel.BasicPublish("order.exchange", "order.order.delivered", body: orderDeliveredPayload);

        var client = factory.CreateClient();

        var canSubmit = await TestDataHelper.WaitUntilAsync(async () =>
        {
            var response = await client.SendAsync(client.PostJson("/v1/reviews", new { orderId, sku, rating = 5, bodyText = "arrived via real rabbitmq" })
                .WithTestAuth(userId).WithIdempotencyKey($"key-{Guid.NewGuid():N}"));
            return response.StatusCode == HttpStatusCode.Created;
        }, TimeSpan.FromSeconds(30));

        canSubmit.Should().BeTrue("the OrderCreated/OrderDelivered consumer must have populated VerifiedPurchaseRecord from real broker messages");
    }
}
