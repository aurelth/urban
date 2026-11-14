using MediatR;
using Urban.SharedKernel;

namespace Urban.Condominium.Application.Residents;

/// <summary>Convida o morador a entrar no sistema: cria o acesso na primeira vez e reenvia o convite nas seguintes.</summary>
public sealed record InviteResidentCommand(Guid ActorUserId, Guid ResidentId) : IRequest<Result>;
