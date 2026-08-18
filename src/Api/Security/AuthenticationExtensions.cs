using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace Kart.Review.Api.Security;

/// <summary>
/// api-contract.yaml's single <c>bearerAuth</c> scheme (Identity-issued, JWKS-verified JWT),
/// distinguished by a <c>roles</c> claim: <c>customer</c> for the author-facing endpoints,
/// <c>support_agent</c>/<c>admin</c> for <c>PATCH /v1/reviews/{id}/moderate</c> (BRD §24.1).
/// Mirrors kart-order-service/kart-payment-service's identically-shaped extension.
/// </summary>
public static class AuthenticationExtensions
{
    public const string ModeratorPolicy = "Moderator";
    private const string RolesClaimType = "roles";

    public static IServiceCollection AddReviewAuthentication(this IServiceCollection services)
    {
        services.AddMemoryCache();
        services.AddHttpClient<JwksSigningKeyResolver>();

        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer();

        services.AddOptions<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme)
            .Configure<JwksSigningKeyResolver>((options, resolver) =>
            {
                // Disable .NET's default inbound claim-type remapping so the raw "roles"/"sub"
                // claim names survive verbatim — the ModeratorPolicy below and HttpCurrentPrincipal
                // both match on them literally.
                options.MapInboundClaims = false;

                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = false,
                    ValidateAudience = false,
                    ValidateLifetime = true,
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKeyResolver = resolver.ResolveSigningKeys,
                };
            });

        services.AddAuthorizationBuilder()
            .AddPolicy(ModeratorPolicy, policy => policy.RequireClaim(RolesClaimType, "support_agent", "admin"));

        return services;
    }
}
