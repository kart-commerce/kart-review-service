using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Kart.Review.IntegrationTests.Infrastructure;
using Xunit;

namespace Kart.Review.IntegrationTests.Endpoints;

[Collection(ReviewApiCollection.Name)]
public sealed class SubmitReviewEndpointTests(ReviewApiFactory factory)
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    [Fact]
    public async Task Post_NoVerifiedPurchase_Returns409WithRetryableCode()
    {
        var client = factory.CreateClient();
        var orderId = Guid.NewGuid();

        var response = await client.SendAsync(client.PostJson("/v1/reviews", new { orderId, sku = "SKU-X", rating = 5, bodyText = "great" })
            .WithTestAuth(Guid.NewGuid())
            .WithIdempotencyKey($"key-{Guid.NewGuid():N}"));

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
        var problem = await response.Content.ReadFromJsonAsync<ProblemDto>(JsonOptions);
        problem!.ErrorCode.Should().Be("verified_purchase_not_found");
    }

    [Fact]
    public async Task Post_CleanContentAfterDelivery_PublishesImmediately_AndEventuallyProjectsToReadModelAndRating()
    {
        var client = factory.CreateClient();
        var userId = Guid.NewGuid();
        var orderId = Guid.NewGuid();
        const string sku = "SKU-CLEAN-1";
        await TestDataHelper.SeedDeliveredPurchaseAsync(factory, orderId, userId, sku);

        var response = await client.SendAsync(client.PostJson("/v1/reviews", new { orderId, sku, rating = 5, bodyText = "Absolutely great product" })
            .WithTestAuth(userId)
            .WithIdempotencyKey($"key-{Guid.NewGuid():N}"));

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var review = await response.Content.ReadFromJsonAsync<ReviewViewDto>(JsonOptions);
        review!.Status.Should().Be("Published");
        review.FirstPublishedAt.Should().NotBeNull();

        var appearedInReadModel = await TestDataHelper.WaitUntilAsync(async () =>
        {
            var listResponse = await client.GetAsync($"/v1/reviews?sku={sku}&page=1&pageSize=20");
            var list = await listResponse.Content.ReadFromJsonAsync<ReviewListViewDto>(JsonOptions);
            return list!.Items.Any(i => i.ReviewId == review.ReviewId);
        });
        appearedInReadModel.Should().BeTrue("REV-5's projector must eventually rebuild the Mongo read model from the published review");

        var ratingUpdated = await TestDataHelper.WaitUntilAsync(async () =>
        {
            var ratingResponse = await client.GetAsync($"/v1/product-ratings/{sku}");
            if (ratingResponse.StatusCode != HttpStatusCode.OK)
            {
                return false;
            }

            var rating = await ratingResponse.Content.ReadFromJsonAsync<ProductRatingViewDto>(JsonOptions);
            return rating!.Count == 1 && rating.Avg == 5;
        });
        ratingUpdated.Should().BeTrue("REV-6's self-consumer must apply ReviewSubmitted to the ProductRating aggregate");
    }

    [Fact]
    public async Task Post_FlaggedContent_QueuesForModeration_NeverAppearsInReadModelOrRating()
    {
        var client = factory.CreateClient();
        var userId = Guid.NewGuid();
        var orderId = Guid.NewGuid();
        const string sku = "SKU-FLAGGED-1";
        await TestDataHelper.SeedDeliveredPurchaseAsync(factory, orderId, userId, sku);

        var response = await client.SendAsync(client.PostJson("/v1/reviews", new { orderId, sku, rating = 1, bodyText = "this is a scam" })
            .WithTestAuth(userId)
            .WithIdempotencyKey($"key-{Guid.NewGuid():N}"));

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var review = await response.Content.ReadFromJsonAsync<ReviewViewDto>(JsonOptions);
        review!.Status.Should().Be("PendingModeration");
        review.FirstPublishedAt.Should().BeNull();

        await Task.Delay(TimeSpan.FromSeconds(3));
        var listResponse = await client.GetAsync($"/v1/reviews?sku={sku}&page=1&pageSize=20");
        var list = await listResponse.Content.ReadFromJsonAsync<ReviewListViewDto>(JsonOptions);
        list!.Items.Should().BeEmpty("a PendingModeration review must never reach the public read model");

        var ratingResponse = await client.GetAsync($"/v1/product-ratings/{sku}");
        ratingResponse.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Post_DuplicateOrderSku_Returns409()
    {
        var client = factory.CreateClient();
        var userId = Guid.NewGuid();
        var orderId = Guid.NewGuid();
        const string sku = "SKU-DUP-1";
        await TestDataHelper.SeedDeliveredPurchaseAsync(factory, orderId, userId, sku);

        var first = await client.SendAsync(client.PostJson("/v1/reviews", new { orderId, sku, rating = 4, bodyText = "first" })
            .WithTestAuth(userId).WithIdempotencyKey($"key-{Guid.NewGuid():N}"));
        first.StatusCode.Should().Be(HttpStatusCode.Created);

        var second = await client.SendAsync(client.PostJson("/v1/reviews", new { orderId, sku, rating = 2, bodyText = "second attempt" })
            .WithTestAuth(userId).WithIdempotencyKey($"key-{Guid.NewGuid():N}"));

        second.StatusCode.Should().Be(HttpStatusCode.Conflict);
        var problem = await second.Content.ReadFromJsonAsync<ProblemDto>(JsonOptions);
        problem!.ErrorCode.Should().Be("duplicate_review");
    }

    [Fact]
    public async Task Post_SameIdempotencyKeySameBody_ReplaysOriginalResponse_NoSecondRowCreated()
    {
        var client = factory.CreateClient();
        var userId = Guid.NewGuid();
        var orderId = Guid.NewGuid();
        const string sku = "SKU-IDEMPOTENT-1";
        await TestDataHelper.SeedDeliveredPurchaseAsync(factory, orderId, userId, sku);
        var idempotencyKey = $"key-{Guid.NewGuid():N}";

        var first = await client.SendAsync(client.PostJson("/v1/reviews", new { orderId, sku, rating = 4, bodyText = "solid" })
            .WithTestAuth(userId).WithIdempotencyKey(idempotencyKey));
        var firstReview = await first.Content.ReadFromJsonAsync<ReviewViewDto>(JsonOptions);

        var replay = await client.SendAsync(client.PostJson("/v1/reviews", new { orderId, sku, rating = 4, bodyText = "solid" })
            .WithTestAuth(userId).WithIdempotencyKey(idempotencyKey));
        var replayedReview = await replay.Content.ReadFromJsonAsync<ReviewViewDto>(JsonOptions);

        replayedReview!.ReviewId.Should().Be(firstReview!.ReviewId);
        (await TestDataHelper.CountReviewsAsync(factory, orderId, sku)).Should().Be(1);
    }

    [Fact]
    public async Task Post_SameIdempotencyKeyDifferentBody_Returns422()
    {
        var client = factory.CreateClient();
        var userId = Guid.NewGuid();
        var orderId = Guid.NewGuid();
        const string sku = "SKU-CONFLICT-1";
        await TestDataHelper.SeedDeliveredPurchaseAsync(factory, orderId, userId, sku);
        var idempotencyKey = $"key-{Guid.NewGuid():N}";

        await client.SendAsync(client.PostJson("/v1/reviews", new { orderId, sku, rating = 4, bodyText = "solid" })
            .WithTestAuth(userId).WithIdempotencyKey(idempotencyKey));

        var conflicting = await client.SendAsync(client.PostJson("/v1/reviews", new { orderId, sku, rating = 1, bodyText = "totally different" })
            .WithTestAuth(userId).WithIdempotencyKey(idempotencyKey));

        conflicting.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
    }

    [Fact]
    public async Task Post_ConcurrentIdenticalRequests_CreatesExactlyOneReview_NoRaceCondition()
    {
        var client = factory.CreateClient();
        var userId = Guid.NewGuid();
        var orderId = Guid.NewGuid();
        const string sku = "SKU-CONCURRENT-1";
        await TestDataHelper.SeedDeliveredPurchaseAsync(factory, orderId, userId, sku);
        var idempotencyKey = $"key-{Guid.NewGuid():N}";

        var tasks = Enumerable.Range(0, 10).Select(_ => client.SendAsync(
            client.PostJson("/v1/reviews", new { orderId, sku, rating = 5, bodyText = "concurrent test" })
                .WithTestAuth(userId).WithIdempotencyKey(idempotencyKey)));

        var responses = await Task.WhenAll(tasks);

        responses.Should().OnlyContain(r => r.StatusCode == HttpStatusCode.Created);
        (await TestDataHelper.CountReviewsAsync(factory, orderId, sku)).Should().Be(1, "10 concurrent identical requests must never create more than one review");

        var reviewIds = new HashSet<Guid>();
        foreach (var response in responses)
        {
            var review = await response.Content.ReadFromJsonAsync<ReviewViewDto>(JsonOptions);
            reviewIds.Add(review!.ReviewId);
        }

        reviewIds.Should().ContainSingle("every concurrent caller must observe the exact same review id");
    }
}
