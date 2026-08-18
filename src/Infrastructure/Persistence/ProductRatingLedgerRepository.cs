using Kart.Review.Application.Common.Interfaces;
using Kart.Review.Domain.Common.ValueObjects;
using Kart.Review.Domain.ProductRatings;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Kart.Review.Infrastructure.Persistence;

/// <summary>
/// REV-6's ledger access. <see cref="GetOrCreateAsync"/> closes a genuine race: two overlapping
/// RabbitMQ deliveries for the same <c>(orderId, sku)</c> (e.g. a fast <c>ReviewSubmitted</c>
/// immediately followed by a <c>ReviewUpdated</c>, dispatched to two different prefetched
/// deliveries) can both observe "no ledger row yet" and both attempt to insert one. Rather than a
/// <c>SELECT ... FOR UPDATE</c> (which EF Core has no first-class API for), this retries on the
/// resulting unique-constraint violation — the same defensive pattern kart-payment-service's
/// <c>EfIdempotencyGuard</c> uses for its own concurrent-reservation race.
/// </summary>
public sealed class ProductRatingLedgerRepository(ReviewDbContext dbContext) : IProductRatingLedgerRepository
{
    private const string PostgresUniqueViolationSqlState = "23505";
    private const int MaxAttempts = 10;

    public async Task<ProductRatingLedgerEntry> GetOrCreateAsync(OrderId orderId, Sku sku, DateTimeOffset now, string principal, CancellationToken cancellationToken)
    {
        for (var attempt = 0; attempt < MaxAttempts; attempt++)
        {
            var existing = await dbContext.ProductRatingLedgerEntries
                .FirstOrDefaultAsync(e => e.OrderId == orderId && e.Sku == sku, cancellationToken);

            if (existing is not null)
            {
                return existing;
            }

            var entry = ProductRatingLedgerEntry.CreateEmpty(orderId, sku, now, principal);
            dbContext.ProductRatingLedgerEntries.Add(entry);

            try
            {
                // Saved immediately — the ledger row must exist and be visible before the caller
                // decides whether/how to mutate ProductRating, and a lost race here must be
                // detected before that decision, not after.
                await dbContext.SaveChangesAsync(cancellationToken);
                return entry;
            }
            catch (DbUpdateException ex) when (ex.InnerException is PostgresException { SqlState: PostgresUniqueViolationSqlState })
            {
                // Lost the race to a concurrent delivery that inserted this exact key first —
                // detach our failed attempt and loop back to read what they created.
                dbContext.Entry(entry).State = EntityState.Detached;
            }
        }

        throw new InvalidOperationException($"Could not resolve a ProductRatingLedgerEntry for ({orderId}, {sku}) after {MaxAttempts} attempts.");
    }
}
