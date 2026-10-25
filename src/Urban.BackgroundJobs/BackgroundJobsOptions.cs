using Hangfire;
using Hangfire.PostgreSql;

namespace Urban.BackgroundJobs;

/// <summary>Configuração das tarefas em segundo plano (seção "BackgroundJobs").</summary>
public sealed class BackgroundJobsOptions
{
    public const string SectionName = "BackgroundJobs";

    public const int MaxWorkerCount = 20;

    private static readonly TimeSpan QueuePollInterval = TimeSpan.FromSeconds(2);

    /// <summary>Se este processo executa as tarefas. Desligado, ele só enfileira: serve para mover a execução para o Urban.Workers.</summary>
    public bool RunServer { get; init; } = true;

    public int WorkerCount { get; init; } = 2;

    /// <exception cref="InvalidOperationException">A configuração é inválida.</exception>
    public void Validate()
    {
        if (WorkerCount is < 1 or > MaxWorkerCount)
        {
            throw new InvalidOperationException($"O número de processadores ({SectionName}:{nameof(WorkerCount)}) precisa estar entre 1 e {MaxWorkerCount}.");
        }
    }

    public void ApplyTo(BackgroundJobServerOptions server) => server.WorkerCount = WorkerCount;

    /// <summary>
    /// A fila vive no schema "hangfire", separado das tabelas dos módulos. O intervalo de consulta de 2 s evita que o e-mail de
    /// confirmação espere o padrão de 15 s. Sem banco no início, a API não sobe: melhor falhar no deploy do que rodar sem fila.
    /// </summary>
    public static PostgreSqlStorageOptions CreateStorageOptions() => new()
    {
        SchemaName = "hangfire",
        PrepareSchemaIfNecessary = true,
        QueuePollInterval = QueuePollInterval,
        AllowDegradedModeWithoutStorage = false,
    };
}