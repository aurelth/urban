namespace Urban.Api.Http;

public static class RouteHandlerBuilderExtensions
{
    /// <summary>Documenta os erros que toda rota protegida pode devolver (401, 403 e 429), mais os da própria rota.</summary>
    public static RouteHandlerBuilder WithErrors(this RouteHandlerBuilder builder, params int[] statuses)
    {
        foreach (int status in new[] { StatusCodes.Status401Unauthorized, StatusCodes.Status403Forbidden, StatusCodes.Status429TooManyRequests }.Concat(statuses).Distinct())
        {
            builder.ProducesProblem(status);
        }

        return builder;
    }
}
