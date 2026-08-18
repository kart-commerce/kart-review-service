using System.Net.Http.Json;

namespace Kart.Review.IntegrationTests.Infrastructure;

public static class HttpClientExtensions
{
    public static HttpRequestMessage WithTestAuth(this HttpRequestMessage request, Guid userId, params string[] roles)
    {
        request.Headers.Add(TestAuthHandler.UserIdHeader, userId.ToString());
        if (roles.Length > 0)
        {
            request.Headers.Add(TestAuthHandler.RolesHeader, string.Join(",", roles));
        }

        return request;
    }

    public static HttpRequestMessage WithIdempotencyKey(this HttpRequestMessage request, string key)
    {
        request.Headers.Add("Idempotency-Key", key);
        return request;
    }

    public static HttpRequestMessage PostJson(this HttpClient _, string url, object body) =>
        new(HttpMethod.Post, url) { Content = JsonContent.Create(body) };

    public static HttpRequestMessage PatchJson(this HttpClient _, string url, object body) =>
        new(HttpMethod.Patch, url) { Content = JsonContent.Create(body) };
}
