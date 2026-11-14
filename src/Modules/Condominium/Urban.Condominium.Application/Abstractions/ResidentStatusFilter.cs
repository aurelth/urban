namespace Urban.Condominium.Application.Abstractions;

/// <summary>
/// Quem a lista de moradores traz quanto a morar ou não. Com uma unidade no filtro, o estado é o da pessoa naquela unidade; sem
/// ela, o do condomínio inteiro.
/// </summary>
public enum ResidentStatusFilter
{
    /// <summary>Qualquer morador, atual ou ex-morador.</summary>
    All,

    /// <summary>Tem ao menos um vínculo em andamento.</summary>
    Active,

    /// <summary>Já teve vínculo, mas nenhum está em andamento.</summary>
    Former,
}
