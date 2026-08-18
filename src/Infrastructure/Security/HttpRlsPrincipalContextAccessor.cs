using Microsoft.AspNetCore.Http;

namespace Kart.Review.Infrastructure.Security;

/// <summary>
/// Scoped per request (or per background unit-of-work, where <see cref="IHttpContextAccessor.HttpContext"/>
/// is null — every hosted service's own <c>IServiceScopeFactory.CreateScope()</c> call creates a
/// fresh DI scope with no ambient HTTP context, so this gracefully falls back to the "service/
/// system" bypass there too, without needing a second implementation) — resolved fresh by
/// <see cref="RlsConnectionInterceptor"/> every time this <c>ReviewDbContext</c> instance's
/// connection is opened, so a pooled physical connection always carries the CURRENT scope's
/// principal, never a stale one left over from whichever request previously used that same socket.
/// </summary>
public sealed class HttpRlsPrincipalContextAccessor(IHttpContextAccessor httpContextAccessor) : IRlsPrincipalContextAccessor
{
    private const string RolesClaimType = "roles";

    public RlsPrincipalContext Current
    {
        get
        {
            var user = httpContextAccessor.HttpContext?.User;
            var sub = user?.FindFirst("sub")?.Value;

            if (sub is null)
            {
                // No authenticated caller in scope — either an anonymous public read (GET
                // endpoints) or a background worker. Neither owns a row by user_id, so this maps
                // to the policies' own "service/system" bypass rather than "user" with a null id.
                return new RlsPrincipalContext(null, null, "service");
            }

            var role = user!.FindFirst(RolesClaimType)?.Value;
            return new RlsPrincipalContext(sub, role, "user");
        }
    }
}
