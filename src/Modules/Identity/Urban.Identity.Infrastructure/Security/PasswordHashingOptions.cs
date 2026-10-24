namespace Urban.Identity.Infrastructure.Security;

/// <summary>Configuração do hash de senhas (seção "PasswordHashing").</summary>
public sealed class PasswordHashingOptions
{
    public const string SectionName = "PasswordHashing";

    /// <summary>Limites do BCrypt: abaixo de 4 ou acima de 31 o algoritmo não aceita.</summary>
    public const int MinimumWorkFactor = 4;

    public const int MaximumWorkFactor = 31;

    /// <summary>Custo do BCrypt em potências de 2: cada +1 dobra o tempo. 12 leva algumas centenas de milissegundos num servidor comum.</summary>
    public int WorkFactor { get; init; } = 12;
}