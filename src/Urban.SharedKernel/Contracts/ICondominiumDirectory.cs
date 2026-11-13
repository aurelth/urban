namespace Urban.SharedKernel.Contracts;

/// <summary>
/// Contrato do módulo Condomínio para os outros módulos: o nome de cada condomínio pelo identificador. Quem lista contas
/// (o painel do administrador, no Identity) mostra o nome real sem conhecer as camadas internas do Condomínio.
/// </summary>
public interface ICondominiumDirectory
{
    /// <returns>O nome de cada condomínio encontrado; identificadores desconhecidos ficam de fora. Vale para a plataforma inteira, não só para o tenant da requisição.</returns>
    Task<IReadOnlyDictionary<Guid, string>> GetNamesAsync(IReadOnlyCollection<Guid> condominiumIds, CancellationToken cancellationToken);
}
