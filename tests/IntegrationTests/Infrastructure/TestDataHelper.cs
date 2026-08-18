using Kart.Review.Domain.Common.ValueObjects;
using Kart.Review.Domain.VerifiedPurchases;
using Kart.Review.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Kart.Review.IntegrationTests.Infrastructure;

/// <summary>Direct-DB seeding/inspection helpers for tests — bypasses the RabbitMQ OrderCreated/OrderDelivered flow where a test's focus is the review-submission logic, not REV-2 itself (that gets its own dedicated messaging test).</summary>
public static class TestDataHelper
{
    public static async Task SeedDeliveredPurchaseAsync(ReviewApiFactory factory, Guid orderId, Guid userId, string sku)
    {
        using var scope = factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ReviewDbContext>();

        var now = DateTimeOffset.UtcNow;
        var record = VerifiedPurchaseRecord.CreateFromOrderCreated(OrderId.From(orderId), UserId.From(userId), [Sku.From(sku)], now, "system:test");
        record.ApplyOrderDelivered(now, now, "system:test");
        dbContext.VerifiedPurchaseRecords.Add(record);
        await dbContext.SaveChangesAsync();
    }

    public static async Task<Domain.Reviews.Review?> GetReviewAsync(ReviewApiFactory factory, Guid reviewId)
    {
        using var scope = factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ReviewDbContext>();
        return await dbContext.Reviews.AsNoTracking().FirstOrDefaultAsync(r => r.Id == reviewId);
    }

    public static async Task SetReviewCreatedAtAsync(ReviewApiFactory factory, Guid reviewId, DateTimeOffset createdAt)
    {
        using var scope = factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ReviewDbContext>();
        await dbContext.Database.ExecuteSqlInterpolatedAsync($"UPDATE reviews SET created_at = {createdAt} WHERE review_id = {reviewId}");
    }

    public static async Task<long> CountReviewsAsync(ReviewApiFactory factory, Guid orderId, string sku)
    {
        using var scope = factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ReviewDbContext>();
        return await dbContext.Reviews.CountAsync(r => r.OrderId == OrderId.From(orderId) && r.Sku == Sku.From(sku));
    }

    /// <summary>Polls until <paramref name="condition"/> is true or the timeout elapses — the async outbox relay/projectors/consumers run on their own poll intervals (2-5s), so assertions on their effects can't be synchronous.</summary>
    public static async Task<bool> WaitUntilAsync(Func<Task<bool>> condition, TimeSpan? timeout = null)
    {
        var deadline = DateTime.UtcNow.Add(timeout ?? TimeSpan.FromSeconds(20));
        while (DateTime.UtcNow < deadline)
        {
            if (await condition())
            {
                return true;
            }

            await Task.Delay(250);
        }

        return false;
    }
}
