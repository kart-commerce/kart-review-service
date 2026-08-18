using Kart.Review.Application.Common.Interfaces;
using Kart.Review.Domain.Common.ValueObjects;
using Microsoft.AspNetCore.Http;

namespace Kart.Review.Infrastructure.Security;

/// <summary>Resolves the authenticated caller from the JWT's <c>sub</c> claim — never taken from a request body (requirement-spec's stated invariant). Only ever resolved within an HTTP request scope; background/system work (RabbitMQ consumers) stamps <see cref="Domain.Common.SystemPrincipals"/> directly instead.</summary>
public sealed class HttpCurrentPrincipal(IHttpContextAccessor httpContextAccessor) : ICurrentPrincipal
{
    private const string RolesClaimType = "roles";

    public UserId UserId
    {
        get
        {
            var subClaim = httpContextAccessor.HttpContext?.User.FindFirst("sub")?.Value
                ?? throw new InvalidOperationException("No authenticated principal (missing 'sub' claim) in the current HTTP context.");
            return UserId.From(Guid.Parse(subClaim));
        }
    }

    public string PrincipalId => UserId.ToString();

    public bool IsInRole(string role) =>
        httpContextAccessor.HttpContext?.User.Claims.Any(c => c.Type == RolesClaimType && string.Equals(c.Value, role, StringComparison.OrdinalIgnoreCase)) ?? false;
}
