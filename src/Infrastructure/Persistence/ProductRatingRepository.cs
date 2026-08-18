using Kart.Review.Application.Common.Interfaces;
using Kart.Review.Domain.Common.ValueObjects;
using Kart.Review.Domain.ProductRatings;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Kart.Review.Infrastructure.Persistence;

public sealed class ProductRatingRepository(ReviewDbContext dbContext) : IProductRatingRepository
{
    private const string PostgresUniqueViolationSqlState = "23505";
    private const int MaxAttempts = 10;

    public Task<ProductRating?> GetBySkuAsync(Sku sku, CancellationToken cancellationToken) =>
        dbContext.ProductRatings.FirstOrDefaultAsync(r => r.Sku == sku, cancellationToken);

    public void Add(ProductRating rating) => dbContext.ProductRatings.Add(rating);

    public async Task<ProductRating> GetOrCreateAsync(Sku sku, DateTimeOffset now, string principal, CancellationToken cancellationToken)
    {
        for (var attempt = 0; attempt < MaxAttempts; attempt++)
        {
            var existing = await dbContext.ProductRatings.FirstOrDefaultAsync(r => r.Sku == sku, cancellationToken);
            if (existing is not null)
            {
                return existing;
            }

            var rating = ProductRating.CreateEmpty(sku, now, principal);
            dbContext.ProductRatings.Add(rating);

            try
            {
                // Saved immediately (not deferred) — this row's PK must be durably committed
                // before any ProductRatingLedgerEntry referencing it via FK is inserted.
                await dbContext.SaveChangesAsync(cancellationToken);
                return rating;
            }
            catch (DbUpdateException ex) when (ex.InnerException is PostgresException { SqlState: PostgresUniqueViolationSqlState })
            {
                // Lost the race to a concurrent delivery creating this exact sku's row first.
                dbContext.Entry(rating).State = EntityState.Detached;
            }
        }

        throw new InvalidOperationException($"Could not resolve a ProductRating for sku '{sku}' after {MaxAttempts} attempts.");
    }
}
