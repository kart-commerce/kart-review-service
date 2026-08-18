using Kart.Review.Domain.Common.ValueObjects;
using Kart.Review.Domain.ProductRatings;

namespace Kart.Review.Application.Common.Interfaces;

/// <summary>
/// The REV-6 consumer's idempotency ledger access. <see cref="GetOrCreateAsync"/> hides the
/// concurrent-insert race two overlapping consumer deliveries for the same <c>(orderId, sku)</c>
/// could trigger (a real, at-least-once-delivery-shaped race, not a hypothetical one) — the
/// Infrastructure implementation retries on a unique-constraint violation rather than surfacing it,
/// so every caller here just sees a plain get-or-create.
/// </summary>
public interface IProductRatingLedgerRepository
{
    Task<ProductRatingLedgerEntry> GetOrCreateAsync(OrderId orderId, Sku sku, DateTimeOffset now, string principal, CancellationToken cancellationToken);
}
