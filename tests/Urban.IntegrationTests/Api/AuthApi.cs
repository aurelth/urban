using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

namespace Urban.IntegrationTests.Api;

internal static class AuthApi
{
    private const string RefreshCookieName = "__Secure-urban-refresh";

    public static Task<HttpResponseMessage> LoginAsync(this HttpClient client, string email, string password) =>
        client.PostAsJsonAsync("/api/auth/login", new { email, password });

    public static Task<HttpResponseMessage> RefreshAsync(this HttpClient client, string? cookie) =>
        client.SendAsync(PostRequest("/api/auth/refresh", accessToken: null, cookie));

    public static Task<HttpResponseMessage> LogoutAsync(this HttpClient client, string? accessToken, string? cookie = null) =>
        client.SendAsync(PostRequest("/api/auth/logout", accessToken, cookie));

    public static async Task<string> CodeAsync(this HttpResponseMessage response)
    {
        JsonElement body = await response.Content.ReadFromJsonAsync<JsonElement>();
        return body.GetProperty("code").GetString()!;
    }

    public static async Task<string> AccessTokenAsync(this HttpResponseMessage response)
    {
        JsonElement body = await response.Content.ReadFromJsonAsync<JsonElement>();
        return body.GetProperty("accessToken").GetString()!;
    }

    public static string? RefreshCookieHeader(this HttpResponseMessage response) =>
        response.Headers.TryGetValues("Set-Cookie", out IEnumerable<string>? values)
            ? values.SingleOrDefault(v => v.StartsWith($"{RefreshCookieName}=", StringComparison.Ordinal))
            : null;

    public static string? RefreshCookieValue(this HttpResponseMessage response)
    {
        string? header = response.RefreshCookieHeader();
        return header is null ? null : header[(RefreshCookieName.Length + 1)..].Split(';')[0];
    }

    private static HttpRequestMessage PostRequest(string path, string? accessToken, string? cookie)
    {
        HttpRequestMessage request = new(HttpMethod.Post, path);
        if (accessToken is not null)
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        }

        if (cookie is not null)
        {
            request.Headers.Add("Cookie", $"{RefreshCookieName}={cookie}");
        }

        return request;
    }
}