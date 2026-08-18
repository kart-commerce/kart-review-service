namespace Kart.Review.Infrastructure.Auditing;

/// <summary>EF-backed row shape for the `audit_log` table — mirrors kart-order-service's `OrderAuditLogEntry`, the platform's first real (non-Null) <c>IAuditLogWriter</c> sink.</summary>
public sealed class ReviewAuditLogEntry
{
    public Guid EntryId { get; private set; }

    public string ServiceName { get; private set; } = string.Empty;

    public string ActorId { get; private set; } = string.Empty;

    public string ActorType { get; private set; } = string.Empty;

    public string Action { get; private set; } = string.Empty;

    public string EntityType { get; private set; } = string.Empty;

    public string EntityId { get; private set; } = string.Empty;

    public DateTimeOffset OccurredAt { get; private set; }

    public string? MetadataJson { get; private set; }

    /// <summary>EF Core materialization only.</summary>
    private ReviewAuditLogEntry()
    {
    }

    public static ReviewAuditLogEntry Create(
        Guid entryId, string serviceName, string actorId, string actorType, string action, string entityType, string entityId, DateTimeOffset occurredAt, string? metadataJson) => new()
    {
        EntryId = entryId,
        ServiceName = serviceName,
        ActorId = actorId,
        ActorType = actorType,
        Action = action,
        EntityType = entityType,
        EntityId = entityId,
        OccurredAt = occurredAt,
        MetadataJson = metadataJson,
    };
}
