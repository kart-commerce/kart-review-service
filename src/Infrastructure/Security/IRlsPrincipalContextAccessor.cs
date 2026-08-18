namespace Kart.Review.Infrastructure.Security;

/// <summary>
/// Resolves the three session variables `ReviewDbContext`'s RLS policies read on every query
/// (database-design.md §"Row-Level Security"): `app.current_principal` (the caller's own
/// `userId`), `app.current_principal_role` (coarse JWT role claim), and
/// `app.current_principal_kind` (`user`/`service`/`system`). HTTP requests resolve these from the
/// JWT; background workers (outbox relay, RabbitMQ consumers, projectors) have no HTTP context and
/// resolve to a well-known `system` kind instead.
/// </summary>
public interface IRlsPrincipalContextAccessor
{
    RlsPrincipalContext Current { get; }
}

public sealed record RlsPrincipalContext(string? PrincipalId, string? Role, string Kind);
