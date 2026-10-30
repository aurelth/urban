using Urban.Condominium.Domain.People;
using CondominiumEntity = Urban.Condominium.Domain.Condominiums.Condominium;

namespace Urban.Condominium.Application.Abstractions;

/// <summary>
/// O cadastro de um síndico como a leitura do banco o entrega: o condomínio, o síndico e a versão dos dois (no PostgreSQL, o
/// <c>xmin</c> das duas linhas juntos). Quem edita devolve a versão que leu; se mudou no meio do caminho, a gravação é recusada.
/// </summary>
public sealed record ManagerCondominium(CondominiumEntity Condominium, Person Manager, string Version);
