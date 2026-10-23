namespace Urban.SharedKernel.Abstractions;

/// <summary>Relógio injetável: o código de negócio nunca lê a hora do sistema direto, para os testes controlarem o tempo.</summary>
public interface IClock
{
    DateTimeOffset UtcNow { get; }
}
