using MediatR;
using Urban.Condominium.Domain.Condominiums;
using Urban.SharedKernel;

namespace Urban.Condominium.Application.Units;

/// <param name="Version">A versão do condomínio que a tela leu (vem no resumo das unidades).</param>
public sealed record ChangeApportionmentMethodCommand(Guid ActorUserId, string? Version, ApportionmentMethod Method) : IRequest<Result<ApportionmentView>>;
