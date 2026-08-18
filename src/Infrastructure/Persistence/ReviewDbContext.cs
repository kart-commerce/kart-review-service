using System.Diagnostics;
using System.Text.Json;
using Kart.Review.Application.Common.Exceptions;
using Kart.Review.Application.Common.Interfaces;
using Kart.Review.Domain.Idempotency;
using Kart.Review.Domain.ProductRatings;
using Kart.Review.Domain.Reviews;
using Kart.Review.Domain.Reviews.Events;
using Kart.Review.Domain.VerifiedPurchases;
using Kart.Review.Infrastructure.Auditing;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Kart.Review.Infrastructure.Persistence;

// Bare "Review" resolves to the Kart.Review namespace segment itself, not Domain.Reviews.Review
// (see ReviewConfiguration.cs's own remarks) — "Domain.Reviews.Review" used below instead.

/// <summary>
/// The write-side Postgres <c>DbContext</c> — source of truth for every aggregate
/// (PLATFORM_BLUEPRINT.md's CQRS standard). <see cref="SaveChangesAsync"/>'s override is the
/// "choke point" that converts a <see cref="Domain.Reviews.Review"/>'s raised domain events into
/// <see cref="ReviewOutboxEvent"/> rows in the SAME transaction as the triggering mutation — the
/// Transactional Outbox pattern, copied from kart-category-service's identically-shaped
/// <c>SaveChangesAsync</c> override (never a separate publish step, never an in-memory bus).
/// </summary>
public sealed class ReviewDbContext(DbContextOptions<ReviewDbContext> options) : DbContext(options), IUnitOfWork
{
    private const string PostgresUniqueViolationSqlState = "23505";
    private const string ReviewOrderSkuUniqueConstraintName = "uq_reviews_order_id_sku";
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public DbSet<Domain.Reviews.Review> Reviews => Set<Domain.Reviews.Review>();

    public DbSet<ReviewOutboxEvent> ReviewOutboxEvents => Set<ReviewOutboxEvent>();

    public DbSet<ProductRating> ProductRatings => Set<ProductRating>();

    public DbSet<ProductRatingLedgerEntry> ProductRatingLedgerEntries => Set<ProductRatingLedgerEntry>();

    public DbSet<VerifiedPurchaseRecord> VerifiedPurchaseRecords => Set<VerifiedPurchaseRecord>();

    public DbSet<IdempotencyRecord> IdempotencyRecords => Set<IdempotencyRecord>();

    public DbSet<ReviewAuditLogEntry> AuditLogEntries => Set<ReviewAuditLogEntry>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(ReviewDbContext).Assembly);
        base.OnModelCreating(modelBuilder);
    }

    public override async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        var traceParent = Activity.Current?.Id;
        var reviewsWithEvents = ChangeTracker.Entries<Domain.Reviews.Review>()
            .Select(e => e.Entity)
            .Where(r => r.DomainEvents.Count > 0)
            .ToList();

        var now = DateTimeOffset.UtcNow;

        foreach (var review in reviewsWithEvents)
        {
            foreach (var domainEvent in review.DomainEvents)
            {
                var (eventType, payloadJson) = ToPayload(domainEvent);
                ReviewOutboxEvents.Add(ReviewOutboxEvent.Create(review.Id, eventType, payloadJson, now, traceParent, review.UpdatedBy));
            }
        }

        try
        {
            var result = await base.SaveChangesAsync(cancellationToken);
            foreach (var review in reviewsWithEvents)
            {
                review.ClearDomainEvents();
            }

            return result;
        }
        catch (DbUpdateException ex) when (IsUniqueViolation(ex, ReviewOrderSkuUniqueConstraintName))
        {
            throw new DuplicateReviewException();
        }
    }

    private static bool IsUniqueViolation(DbUpdateException exception, string constraintName) =>
        exception.InnerException is PostgresException { SqlState: PostgresUniqueViolationSqlState } postgresException
        && postgresException.ConstraintName == constraintName;

    private static (string EventType, string PayloadJson) ToPayload(Kart.Shared.Domain.IDomainEvent domainEvent) => domainEvent switch
    {
        ReviewSubmittedDomainEvent e => (
            "ReviewSubmitted",
            JsonSerializer.Serialize(new ReviewSubmittedPayload(e.OrderId.Value, e.Sku.Value, e.Rating.Value, e.ReviewId.Value, e.UserId.Value), JsonOptions)),
        ReviewUpdatedDomainEvent e => (
            "ReviewUpdated",
            JsonSerializer.Serialize(new ReviewUpdatedPayload(e.OrderId.Value, e.Sku.Value, e.OldRating.Value, e.NewRating.Value), JsonOptions)),
        ReviewUnpublishedDomainEvent e => (
            "ReviewUnpublished",
            JsonSerializer.Serialize(new ReviewUnpublishedPayload(e.OrderId.Value, e.Sku.Value, e.Rating.Value, e.ReviewId.Value, e.UserId.Value, e.Reason.ToString()), JsonOptions)),
        _ => throw new InvalidOperationException($"Unknown domain event type '{domainEvent.GetType().Name}' — no outbox payload mapping registered."),
    };
}
