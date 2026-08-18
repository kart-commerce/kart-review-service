using FluentAssertions;
using Kart.Review.Domain.Common.ValueObjects;
using Kart.Review.Domain.VerifiedPurchases;
using Xunit;

namespace Kart.Review.UnitTests.Domain.VerifiedPurchases;

public sealed class VerifiedPurchaseRecordTests
{
    private static readonly DateTimeOffset Now = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
    private static readonly OrderId AnOrderId = OrderId.From(Guid.NewGuid());
    private static readonly UserId AUserId = UserId.From(Guid.NewGuid());
    private static readonly Sku ASku = Sku.From("SKU-1");

    [Fact]
    public void GrantsAccessTo_BeforeOrderDelivered_ReturnsFalse()
    {
        var record = VerifiedPurchaseRecord.CreateFromOrderCreated(AnOrderId, AUserId, [ASku], Now, "system");

        record.GrantsAccessTo(AUserId, ASku).Should().BeFalse("no OrderDelivered has been consumed yet");
    }

    [Fact]
    public void GrantsAccessTo_AfterBothEventsRegardlessOfArrivalOrder_ReturnsTrue()
    {
        // OrderDelivered arriving first — ADR-0021's ordering race: no cross-routing-key guarantee.
        var record = VerifiedPurchaseRecord.CreateFromOrderDelivered(AnOrderId, Now, Now, "system");
        record.GrantsAccessTo(AUserId, ASku).Should().BeFalse("userId/skus not populated yet");

        record.ApplyOrderCreated(AUserId, [ASku], Now, "system");

        record.GrantsAccessTo(AUserId, ASku).Should().BeTrue();
    }

    [Fact]
    public void GrantsAccessTo_WrongUser_ReturnsFalse()
    {
        var record = VerifiedPurchaseRecord.CreateFromOrderCreated(AnOrderId, AUserId, [ASku], Now, "system");
        record.ApplyOrderDelivered(Now, Now, "system");

        record.GrantsAccessTo(UserId.From(Guid.NewGuid()), ASku).Should().BeFalse();
    }

    [Fact]
    public void GrantsAccessTo_SkuNotInOrder_ReturnsFalse()
    {
        var record = VerifiedPurchaseRecord.CreateFromOrderCreated(AnOrderId, AUserId, [ASku], Now, "system");
        record.ApplyOrderDelivered(Now, Now, "system");

        record.GrantsAccessTo(AUserId, Sku.From("OTHER-SKU")).Should().BeFalse();
    }

    [Fact]
    public void ApplyOrderCreated_ReappliedWithSameValues_IsIdempotent()
    {
        var record = VerifiedPurchaseRecord.CreateFromOrderCreated(AnOrderId, AUserId, [ASku], Now, "system");
        record.ApplyOrderDelivered(Now, Now, "system");

        record.ApplyOrderCreated(AUserId, [ASku], Now.AddMinutes(1), "system");

        record.GrantsAccessTo(AUserId, ASku).Should().BeTrue("re-delivery of the same event must remain a no-op-equivalent overwrite");
    }
}
