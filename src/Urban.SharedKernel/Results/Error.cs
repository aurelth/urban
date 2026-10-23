namespace Urban.SharedKernel;

/// <summary>Falha de negócio previsível: código estável para o cliente e descrição para o usuário.</summary>
public sealed record Error(string Code, string Description)
{
    /// <summary>Ausência de erro; só aparece em resultados de sucesso.</summary>
    public static readonly Error None = new(string.Empty, string.Empty);
}
