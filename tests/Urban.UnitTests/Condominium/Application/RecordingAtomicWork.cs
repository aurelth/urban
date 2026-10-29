using Urban.SharedKernel;
using Urban.SharedKernel.Abstractions;

namespace Urban.UnitTests.Condominium.Application;

/// <summary>Executa o trabalho na hora e anota se ele rodou e o que devolveu, como o decorador real faria antes do commit ou do rollback.</summary>
public sealed class RecordingAtomicWork : IAtomicWork
{
    public int Runs { get; private set; }

    public Result? Outcome { get; private set; }

    public async Task<Result> ExecuteAsync(Func<CancellationToken, Task<Result>> work, CancellationToken cancellationToken)
    {
        Runs++;
        Outcome = await work(cancellationToken);

        return Outcome;
    }
}
