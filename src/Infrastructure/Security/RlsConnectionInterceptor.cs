using Microsoft.EntityFrameworkCore.Diagnostics;
using Npgsql;

namespace Kart.Review.Infrastructure.Security;

/// <summary>
/// Re-asserts the three RLS session variables (database-design.md's Row-Level Security section)
/// every time `ReviewDbContext`'s connection is physically opened. Registered as a scoped instance
/// resolved per-DbContext (one per request/unit-of-work) via `AddDbContext`'s `(sp, options)`
/// overload — since <see cref="DbConnectionInterceptor.ConnectionOpened"/> fires on every logical
/// <c>Open()</c> call, and EF opens a fresh logical connection once per scoped
/// <c>ReviewDbContext</c> instance, this always re-applies the CURRENT scope's principal before
/// any query runs — overwriting whatever a previous tenant of that pooled physical socket last
/// set, so Npgsql's connection pooling can never leak one request's principal into another's
/// query. `set_config(..., is_local: false)` (session-scoped, not transaction-scoped `SET LOCAL`)
/// is deliberate here for exactly that reason — it must persist for every command this connection
/// runs during this scope's lifetime, not just one transaction.
/// </summary>
public sealed class RlsConnectionInterceptor(IRlsPrincipalContextAccessor principalContextAccessor) : DbConnectionInterceptor
{
    public override void ConnectionOpened(System.Data.Common.DbConnection connection, ConnectionEndEventData eventData)
    {
        Apply((NpgsqlConnection)connection);
        base.ConnectionOpened(connection, eventData);
    }

    public override async Task ConnectionOpenedAsync(System.Data.Common.DbConnection connection, ConnectionEndEventData eventData, CancellationToken cancellationToken = default)
    {
        await ApplyAsync((NpgsqlConnection)connection, cancellationToken);
        await base.ConnectionOpenedAsync(connection, eventData, cancellationToken);
    }

    private void Apply(NpgsqlConnection connection)
    {
        using var command = BuildCommand(connection);
        command.ExecuteNonQuery();
    }

    private async Task ApplyAsync(NpgsqlConnection connection, CancellationToken cancellationToken)
    {
        await using var command = BuildCommand(connection);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private NpgsqlCommand BuildCommand(NpgsqlConnection connection)
    {
        var context = principalContextAccessor.Current;
        var command = connection.CreateCommand();
        command.CommandText = "SELECT set_config('app.current_principal', @principal, false), " +
                               "set_config('app.current_principal_role', @role, false), " +
                               "set_config('app.current_principal_kind', @kind, false);";
        command.Parameters.AddWithValue("principal", (object?)context.PrincipalId ?? DBNull.Value);
        command.Parameters.AddWithValue("role", (object?)context.Role ?? DBNull.Value);
        command.Parameters.AddWithValue("kind", context.Kind);
        return command;
    }
}
