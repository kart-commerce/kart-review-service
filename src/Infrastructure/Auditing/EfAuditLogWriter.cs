using System.Text.Json;
using Kart.Review.Infrastructure.Persistence;
using Kart.Shared.Auditing;

namespace Kart.Review.Infrastructure.Auditing;

/// <summary>The real, DB-backed <see cref="IAuditLogWriter"/> sink — mirrors kart-order-service's <c>EfAuditLogWriter</c>. Writes and commits immediately (its own <c>SaveChangesAsync</c> call), independent of whatever unit-of-work commit the triggering handler performs.</summary>
public sealed class EfAuditLogWriter(ReviewDbContext dbContext) : IAuditLogWriter
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);

    public async Task WriteAsync(AuditLogEntry entry, CancellationToken cancellationToken = default)
    {
        var metadataJson = entry.Metadata is null ? null : JsonSerializer.Serialize(entry.Metadata, SerializerOptions);

        dbContext.AuditLogEntries.Add(ReviewAuditLogEntry.Create(
            entry.EntryId, entry.ServiceName, entry.ActorId, entry.ActorType, entry.Action, entry.EntityType, entry.EntityId, entry.OccurredAt, metadataJson));

        await dbContext.SaveChangesAsync(cancellationToken);
    }
}
