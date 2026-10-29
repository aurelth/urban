using Microsoft.AspNetCore.Diagnostics;

namespace Urban.Api.Http;

/// <summary>
/// Corpo que não dá para ler (JSON quebrado, data fora do formato) é erro de quem chamou, não da API. Por padrão o framework
/// responde 400 sem código de erro em produção e deixa a exceção virar 500 em desenvolvimento. A API pede que ele sempre
/// lance (ThrowOnBadRequest) e este tratador responde igual nos dois ambientes, com o mesmo código e sem devolver o que foi recebido.
/// </summary>
public sealed class BadRequestExceptionHandler : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        if (exception is not BadHttpRequestException)
        {
            return false;
        }

        await ErrorHttp.ToProblem(RequestErrors.Invalid).ExecuteAsync(httpContext);
        return true;
    }
}
