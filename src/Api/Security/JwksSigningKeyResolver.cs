using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;

namespace Kart.Review.Api.Security;

/// <summary>
/// Resolves RS256 signing keys for validating an Identity-issued access token against
/// kart-identity-service's <c>GET /.well-known/jwks.json</c>. Mirrors kart-order-service's/
/// kart-payment-service's identically-shaped resolver. JwtBearer's <c>IssuerSigningKeyResolver</c>
/// delegate is synchronous, so the in-memory cache keeps the blocking fetch to once per
/// <see cref="CacheDuration"/>.
/// </summary>
public sealed class JwksSigningKeyResolver
{
    private const string CacheKey = "identity-jwks";
    private static readonly TimeSpan CacheDuration = TimeSpan.FromMinutes(10);

    private readonly HttpClient _httpClient;
    private readonly IMemoryCache _cache;
    private readonly string _jwksUri;

    public JwksSigningKeyResolver(HttpClient httpClient, IMemoryCache cache, IConfiguration configuration)
    {
        _httpClient = httpClient;
        _cache = cache;
        _jwksUri = configuration["Identity:JwksUri"]
            ?? throw new InvalidOperationException("Identity:JwksUri is not configured.");
    }

    public IEnumerable<SecurityKey> ResolveSigningKeys(
        string token,
        SecurityToken securityToken,
        string kid,
        TokenValidationParameters validationParameters)
    {
        var keySet = _cache.GetOrCreate(CacheKey, entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = CacheDuration;
            return FetchJwksAsync().GetAwaiter().GetResult();
        });

        return keySet?.Keys ?? Enumerable.Empty<SecurityKey>();
    }

    private async Task<JsonWebKeySet> FetchJwksAsync()
    {
        var response = await _httpClient.GetAsync(_jwksUri);
        response.EnsureSuccessStatusCode();
        var json = await response.Content.ReadAsStringAsync();
        return new JsonWebKeySet(json);
    }
}
