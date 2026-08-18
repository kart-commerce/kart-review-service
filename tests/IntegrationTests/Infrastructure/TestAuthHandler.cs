using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Kart.Review.IntegrationTests.Infrastructure;

/// <summary>
/// Replaces the real JWKS-verified JWT bearer scheme for tests — no live kart-identity-service to
/// issue/verify tokens against. Reads plain test headers instead of a signed token: `Test-UserId`
/// (required for an authenticated request; its absence means anonymous) and `Test-Roles`
/// (comma-separated, optional) — the exact claims `HttpCurrentPrincipal`/`AuthenticationExtensions.ModeratorPolicy`
/// already read (`sub`, `roles`), so the whole authorization/ownership/RLS pipeline is exercised
/// for real, only the token-verification step itself is swapped out.
/// </summary>
public sealed class TestAuthHandler(IOptionsMonitor<AuthenticationSchemeOptions> options, ILoggerFactory logger, UrlEncoder encoder)
    : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    public const string SchemeName = "Test";
    public const string UserIdHeader = "Test-UserId";
    public const string RolesHeader = "Test-Roles";

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (!Request.Headers.TryGetValue(UserIdHeader, out var userId) || string.IsNullOrWhiteSpace(userId))
        {
            return Task.FromResult(AuthenticateResult.NoResult());
        }

        var claims = new List<Claim> { new("sub", userId!) };

        if (Request.Headers.TryGetValue(RolesHeader, out var roles))
        {
            claims.AddRange(roles.ToString().Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).Select(r => new Claim("roles", r)));
        }

        var identity = new ClaimsIdentity(claims, SchemeName);
        var principal = new ClaimsPrincipal(identity);
        var ticket = new AuthenticationTicket(principal, SchemeName);
        return Task.FromResult(AuthenticateResult.Success(ticket));
    }
}
