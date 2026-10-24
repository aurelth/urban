using Microsoft.AspNetCore.Diagnostics;
using Urban.Identity.Application.Abstractions;
using Urban.Identity.Application.Authentication;

namespace Urban.Api.Http;

public sealed class SessionStoreExceptionHandler : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        if (exception is not SessionStoreUnavailableException)
        {
            return false;
        }

        await ErrorHttp.ToProblem(SessionErrors.StoreUnavailable).ExecuteAsync(httpContext);
        return true;
    }
}