using Kart.Review.Domain.Common.ValueObjects;
using Kart.Review.Domain.VerifiedPurchases;
using Kart.Review.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Kart.Review.Infrastructure.Seeding;

/// <summary>
/// Idempotent, config-gated dev-data seed — mirrors kart-identity-service's
/// <c>ServicePrincipalSeeder</c> shape: a static <c>SeedAsync(IServiceProvider, IConfiguration,
/// CancellationToken)</c> entry point, called once from <c>Program.cs</c> right after
/// <c>app.Build()</c>, safe to run unconditionally on every boot (skips any entry whose natural
/// key already exists). Off by default — only populated by setting
/// <c>DevSeed:Enabled=true</c> (local/dev/docker-compose only, never a real environment's
/// GlobalConfig file) — so a fresh local stack has a delivered order to submit a review against
/// without hand-crafting <c>OrderCreated</c>/<c>OrderDelivered</c> events first.
/// </summary>
public static class DevDataSeeder
{
    public static async Task SeedAsync(IServiceProvider services, IConfiguration configuration, CancellationToken cancellationToken)
    {
        if (!configuration.GetValue<bool>("DevSeed:Enabled"))
        {
            return;
        }

        using var scope = services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ReviewDbContext>();

        var seedOrderId = OrderId.From(Guid.Parse("11111111-1111-1111-1111-111111111111"));
        var exists = await dbContext.VerifiedPurchaseRecords.AnyAsync(r => r.OrderId == seedOrderId, cancellationToken);
        if (exists)
        {
            return;
        }

        var seedUserId = UserId.From(Guid.Parse("22222222-2222-2222-2222-222222222222"));
        var seedSku = Sku.From("DEV-SKU-1");
        var now = DateTimeOffset.UtcNow;

        var record = VerifiedPurchaseRecord.CreateFromOrderCreated(seedOrderId, seedUserId, [seedSku], now, "system:dev-seed");
        record.ApplyOrderDelivered(now, now, "system:dev-seed");

        dbContext.VerifiedPurchaseRecords.Add(record);
        await dbContext.SaveChangesAsync(cancellationToken);
    }
}
