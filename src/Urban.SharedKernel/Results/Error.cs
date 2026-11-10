namespace Urban.SharedKernel;

/// <summary>
/// Falha de negócio previsível: código estável para o cliente e descrição para o usuário.
/// <paramref name="Data"/> leva os números e nomes que explicam a falha (quanto falta, quais unidades) e vai ao cliente no campo
/// <c>data</c> do problema; a interface monta a frase a partir deles.
/// </summary>
public sealed record Error(string Code, string Description, IReadOnlyDictionary<string, object?>? Data = null)
{
    /// <summary>Ausência de erro; só aparece em resultados de sucesso.</summary>
    public static readonly Error None = new(string.Empty, string.Empty);
}
