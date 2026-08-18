using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Kart.Review.IntegrationTests.Infrastructure;
using Xunit;

namespace Kart.Review.IntegrationTests.Endpoints;

[Collection(ReviewApiCollection.Name)]
public sealed class EditReviewEndpointTests(ReviewApiFactory factory)
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
    public async Task Patch_ByNonAuthor_Returns403()
    {
        var client = factory.CreateClient();
        var author = Guid.NewGuid();
        var review = await SubmitAsync(client, author, Guid.NewGuid(), "SKU-EDIT-1", 3, "original");

        var response = await client.SendAsync(client.PatchJson($"/v1/reviews/{review.ReviewId}", new { rating = 5 })
            .WithTestAuth(Guid.NewGuid()).WithIdempotencyKey($"key-{Guid.NewGuid():N}"));

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Patch_PublishedReview_ClearedContent_RatingChanges_UpdatesImmediatelyAndAdjustsRating()
    {
        var client = factory.CreateClient();
        var userId = Guid.NewGuid();
        const string sku = "SKU-EDIT-2";
        var review = await SubmitAsync(client, userId, Guid.NewGuid(), sku, 3, "original text");

        await TestDataHelper.WaitUntilAsync(async () =>
        {
            var r = await client.GetAsync($"/v1/product-ratings/{sku}");
            return r.StatusCode == HttpStatusCode.OK;
        });

        var response = await client.SendAsync(client.PatchJson($"/v1/reviews/{review.ReviewId}", new { rating = 5, bodyText = "revised text" })
            .WithTestAuth(userId).WithIdempotencyKey($"key-{Guid.NewGuid():N}"));

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var updated = await response.Content.ReadFromJsonAsync<ReviewViewDto>(JsonOptions);
        updated!.Rating.Should().Be(5);
        updated.BodyText.Should().Be("revised text");

        var ratingAdjusted = await TestDataHelper.WaitUntilAsync(async () =>
        {
            var r = await client.GetAsync($"/v1/product-ratings/{sku}");
            var rating = await r.Content.ReadFromJsonAsync<ProductRatingViewDto>(JsonOptions);
            return rating!.Avg == 5;
        });
        ratingAdjusted.Should().BeTrue("ReviewUpdated must adjust ProductRating's running average");
    }

    [Fact]
    public async Task Patch_PublishedReview_FlaggedContent_StagesPendingRevision_LeavesPublicContentUntouched()
    {
        var client = factory.CreateClient();
        var userId = Guid.NewGuid();
        const string sku = "SKU-EDIT-3";
        var review = await SubmitAsync(client, userId, Guid.NewGuid(), sku, 3, "original text");

        var response = await client.SendAsync(client.PatchJson($"/v1/reviews/{review.ReviewId}", new { rating = 1, bodyText = "this is a scam" })
            .WithTestAuth(userId).WithIdempotencyKey($"key-{Guid.NewGuid():N}"));

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var updated = await response.Content.ReadFromJsonAsync<ReviewViewDto>(JsonOptions);
        updated!.Status.Should().Be("Published");
        updated.Rating.Should().Be(3, "the public content must stay untouched while a revision is staged");
        updated.PendingRevision.Should().NotBeNull();
        updated.PendingRevision!.NewRating.Should().Be(1);
    }

    [Fact]
    public async Task Patch_AfterEditWindowCloses_Returns409()
    {
        var client = factory.CreateClient();
        var userId = Guid.NewGuid();
        var review = await SubmitAsync(client, userId, Guid.NewGuid(), "SKU-EDIT-4", 3, "original");
        await TestDataHelper.SetReviewCreatedAtAsync(factory, review.ReviewId, DateTimeOffset.UtcNow.AddDays(-31));

        var response = await client.SendAsync(client.PatchJson($"/v1/reviews/{review.ReviewId}", new { rating = 5 })
            .WithTestAuth(userId).WithIdempotencyKey($"key-{Guid.NewGuid():N}"));

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
        var problem = await response.Content.ReadFromJsonAsync<ProblemDto>(JsonOptions);
        problem!.ErrorCode.Should().Be("edit_window_closed");
    }
}
