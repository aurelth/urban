using Urban.SharedKernel;

namespace Urban.Api.Http;

public static class ApiResults
{
    /// <summary>Falha vira o problema do erro; sucesso vira 200 com a resposta, que nunca é guardada em cache (traz versão e dados do condomínio).</summary>
    public static IResult Respond<T, TResponse>(Result<T> result, HttpResponse response, Func<T, TResponse> toResponse)
    {
        if (result.IsFailure)
        {
            return ErrorHttp.ToProblem(result.Error);
        }

        response.PreventCaching();

        return Results.Ok(toResponse(result.Value));
    }

    public static IResult Created<T, TResponse>(Result<T> result, HttpResponse response, Func<T, string> location, Func<T, TResponse> toResponse)
    {
        if (result.IsFailure)
        {
            return ErrorHttp.ToProblem(result.Error);
        }

        response.PreventCaching();

        return Results.Created(location(result.Value), toResponse(result.Value));
    }
}
