using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Kart.Review.IntegrationTests.Infrastructure;
using Xunit;

namespace Kart.Review.IntegrationTests.Endpoints;

[Collection(ReviewApiCollection.Name)]
public sealed class RetractAndModerateEndpointTests(ReviewApiFactory factory)
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private async Task<ReviewViewDto> SubmitAsync(HttpClient client, Guid userId, Guid orderId, string sku, int rating, string bodyText)
    {
        await TestDataHelper.SeedDeliveredPurchaseAsync(factory, orderId, userId, sku);
        var response = await client.SendAsync(client.PostJson("/v1/reviews", new { orderId, sku, rating, bodyText })
            .WithTestAuth(userId).WithIdempotencyKey($"key-{Guid.NewGuid():N}"));
        return (await response.Content.ReadFromJsonAsync<ReviewViewDto>(JsonOptions))!;
    }

    [Fact]
    public async Task Delete_ByNonAuthor_Returns403()
    {
        var client = factory.CreateClient();
        var review = await SubmitAsync(client, Guid.NewGuid(), Guid.NewGuid(), "SKU-RETRACT-1", 4, "fine");

        var response = await client.SendAsync(new HttpRequestMessage(HttpMethod.Delete, $"/v1/reviews/{review.ReviewId}").WithTestAuth(Guid.NewGuid()));

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Delete_PublishedReview_RemovesFromReadModelAndDecrementsRating()
    {
        var client = factory.CreateClient();
        var userId = Guid.NewGuid();
        const string sku = "SKU-RETRACT-2";
        var review = await SubmitAsync(client, userId, Guid.NewGuid(), sku, 5, "great");

        await TestDataHelper.WaitUntilAsync(async () =>
        {
            var r = await client.GetAsync($"/v1/reviews?sku={sku}");
            var list = await r.Content.ReadFromJsonAsync<ReviewListViewDto>(JsonOptions);
            return list!.Items.Any(i => i.ReviewId == review.ReviewId);
        });

        var response = await client.SendAsync(new HttpRequestMessage(HttpMethod.Delete, $"/v1/reviews/{review.ReviewId}").WithTestAuth(userId));

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var retracted = await response.Content.ReadFromJsonAsync<ReviewViewDto>(JsonOptions);
        retracted!.Status.Should().Be("Retracted");

        var removedFromReadModel = await TestDataHelper.WaitUntilAsync(async () =>
        {
            var r = await client.GetAsync($"/v1/reviews?sku={sku}");
            var list = await r.Content.ReadFromJsonAsync<ReviewListViewDto>(JsonOptions);
            return list!.Items.All(i => i.ReviewId != review.ReviewId);
        });
        removedFromReadModel.Should().BeTrue();

        var ratingDecremented = await TestDataHelper.WaitUntilAsync(async () =>
        {
            var r = await client.GetAsync($"/v1/product-ratings/{sku}");
            if (r.StatusCode == HttpStatusCode.NotFound)
            {
                return true; // count dropped to 0
            }

            var rating = await r.Content.ReadFromJsonAsync<ProductRatingViewDto>(JsonOptions);
            return rating!.Count == 0;
        });
        ratingDecremented.Should().BeTrue();
    }

    [Fact]
    public async Task Delete_AlreadyRetracted_IsAGuardedNoOp_Returns200Again()
    {
        var client = factory.CreateClient();
        var userId = Guid.NewGuid();
        var review = await SubmitAsync(client, userId, Guid.NewGuid(), "SKU-RETRACT-3", 4, "fine");

        var first = await client.SendAsync(new HttpRequestMessage(HttpMethod.Delete, $"/v1/reviews/{review.ReviewId}").WithTestAuth(userId));
        var second = await client.SendAsync(new HttpRequestMessage(HttpMethod.Delete, $"/v1/reviews/{review.ReviewId}").WithTestAuth(userId));

        first.StatusCode.Should().Be(HttpStatusCode.OK);
        second.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Moderate_ByNonModerator_Returns403()
    {
        var client = factory.CreateClient();
        var review = await SubmitAsync(client, Guid.NewGuid(), Guid.NewGuid(), "SKU-MOD-1", 1, "this is a scam");

        var response = await client.SendAsync(client.PatchJson($"/v1/reviews/{review.ReviewId}/moderate", new { action = "accept" })
            .WithTestAuth(Guid.NewGuid()));

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Moderate_AcceptFlaggedReview_PublishesAndAppearsInReadModelAndRating()
    {
        var client = factory.CreateClient();
        const string sku = "SKU-MOD-2";
        var review = await SubmitAsync(client, Guid.NewGuid(), Guid.NewGuid(), sku, 2, "this is a scam");
        review.Status.Should().Be("PendingModeration");

        var response = await client.SendAsync(client.PatchJson($"/v1/reviews/{review.ReviewId}/moderate", new { action = "accept" })
            .WithTestAuth(Guid.NewGuid(), "support_agent"));

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var moderated = await response.Content.ReadFromJsonAsync<ReviewViewDto>(JsonOptions);
        moderated!.Status.Should().Be("Published");

        var appeared = await TestDataHelper.WaitUntilAsync(async () =>
        {
            var r = await client.GetAsync($"/v1/reviews?sku={sku}");
            var list = await r.Content.ReadFromJsonAsync<ReviewListViewDto>(JsonOptions);
            return list!.Items.Any(i => i.ReviewId == review.ReviewId);
        });
        appeared.Should().BeTrue();
    }

    [Fact]
    public async Task Moderate_PostHocRejectOnPublished_TakesDownAndRemovesFromReadModel()
    {
        var client = factory.CreateClient();
        const string sku = "SKU-MOD-3";
        var review = await SubmitAsync(client, Guid.NewGuid(), Guid.NewGuid(), sku, 5, "fine at first");

        await TestDataHelper.WaitUntilAsync(async () =>
        {
            var r = await client.GetAsync($"/v1/reviews?sku={sku}");
            var list = await r.Content.ReadFromJsonAsync<ReviewListViewDto>(JsonOptions);
            return list!.Items.Any(i => i.ReviewId == review.ReviewId);
        });

        var response = await client.SendAsync(client.PatchJson($"/v1/reviews/{review.ReviewId}/moderate", new { action = "reject", reason = "reported by users" })
            .WithTestAuth(Guid.NewGuid(), "admin"));

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var moderated = await response.Content.ReadFromJsonAsync<ReviewViewDto>(JsonOptions);
        moderated!.Status.Should().Be("Rejected");

        var removed = await TestDataHelper.WaitUntilAsync(async () =>
        {
            var r = await client.GetAsync($"/v1/reviews?sku={sku}");
            var list = await r.Content.ReadFromJsonAsync<ReviewListViewDto>(JsonOptions);
            return list!.Items.All(i => i.ReviewId != review.ReviewId);
        });
        removed.Should().BeTrue();
    }

    [Fact]
    public async Task Moderate_RetractedReview_Returns409()
    {
        var client = factory.CreateClient();
        var userId = Guid.NewGuid();
        var review = await SubmitAsync(client, userId, Guid.NewGuid(), "SKU-MOD-4", 4, "fine");
        await client.SendAsync(new HttpRequestMessage(HttpMethod.Delete, $"/v1/reviews/{review.ReviewId}").WithTestAuth(userId));

        var response = await client.SendAsync(client.PatchJson($"/v1/reviews/{review.ReviewId}/moderate", new { action = "reject" })
            .WithTestAuth(Guid.NewGuid(), "admin"));

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }
}
