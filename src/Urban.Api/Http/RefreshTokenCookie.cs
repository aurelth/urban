namespace Urban.Api.Http;

public static class RefreshTokenCookie
{
    public const string Name = "__Secure-urban-refresh";

    public const string Path = "/api/auth";

    public static void Append(HttpResponse response, string token, DateTimeOffset expiresAt) =>
        response.Cookies.Append(Name, token, Options(expiresAt));

    public static void Delete(HttpResponse response) => response.Cookies.Delete(Name, Options(expiresAt: null));

    public static string? Read(HttpRequest request) => request.Cookies[Name];

    private static CookieOptions Options(DateTimeOffset? expiresAt) => new()
    {
        HttpOnly = true,
        Secure = true,
        SameSite = SameSiteMode.Strict,
        Path = Path,
        Expires = expiresAt,
        IsEssential = true,
    };
}