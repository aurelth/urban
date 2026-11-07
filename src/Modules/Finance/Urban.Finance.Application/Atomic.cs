using Urban.SharedKernel;
using Urban.SharedKernel.Abstractions;

namespace Urban.Finance.Application;

internal static class Atomic
{
    /// <summary>Roda o trabalho na unidade atômica e devolve o resultado dele; se a unidade atômica falhar por outro motivo, devolve essa falha.</summary>
    public static async Task<Result<T>> RunAsync<T>(IAtomicWork atomicWork, Func<CancellationToken, Task<Result<T>>> work, CancellationToken cancellationToken)
    {
        Result<T>? outcome = null;
        Result atomic = await atomicWork.ExecuteAsync(
            async ct =>
            {
                outcome = await work(ct);
                return outcome;
            },
            cancellationToken);

        return atomic.IsSuccess ? outcome! : Result.Failure<T>(atomic.Error);
    }
}
