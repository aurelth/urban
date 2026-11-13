using System.Net.Http.Headers;

namespace Urban.IntegrationTests.Api;

public static class FormRequests
{
    /// <summary>Envia um <c>multipart/form-data</c> com o texto em <c>body</c> e cada arquivo em <c>attachments</c>.</summary>
    public static Task<HttpResponseMessage> SendFormAsync(
        this HttpClient client,
        string route,
        string? accessToken,
        string? body,
        params (string FileName, byte[] Content)[] files)
    {
        MultipartFormDataContent form = new();
        if (body is not null)
        {
            form.Add(new StringContent(body), "body");
        }

        foreach ((string fileName, byte[] content) in files)
        {
            form.Add(new ByteArrayContent(content), "attachments", fileName);
        }

        HttpRequestMessage request = new(HttpMethod.Post, route) { Content = form };
        if (accessToken is not null)
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        }

        return client.SendAsync(request);
    }
}
