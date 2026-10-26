namespace Urban.Api.Http;

public static class HttpResponseExtensions
{
    /// <summary>Proíbe navegador e proxies de guardar a resposta (dados de sessão ou de versão que não podem ficar velhos).</summary>
    public static void PreventCaching(this HttpResponse response)
    {
        response.Headers.CacheControl = "no-store";
        response.Headers.Pragma = "no-cache";
    }
}