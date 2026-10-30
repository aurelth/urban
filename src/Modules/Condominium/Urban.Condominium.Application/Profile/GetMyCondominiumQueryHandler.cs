using MediatR;
using Urban.Condominium.Application.Abstractions;
using Urban.Condominium.Domain.Condominiums;
using Urban.SharedKernel;
using Urban.SharedKernel.Contracts;

namespace Urban.Condominium.Application.Profile;

/// <summary>Devolve o cadastro do síndico logado. O usuário vem do token, nunca da rota: ninguém lê o cadastro de outra pessoa.</summary>
public sealed class GetMyCondominiumQueryHandler(ICondominiumProfiles profiles, IManagerProfile managerProfile)
    : IRequestHandler<GetMyCondominiumQuery, Result<MyCondominium>>
{
    public async Task<Result<MyCondominium>> Handle(GetMyCondominiumQuery request, CancellationToken cancellationToken)
    {
        ManagerCondominium? registration = await profiles.FindOfManagerAsync(request.ManagerUserId, cancellationToken);
        if (registration is null)
        {
            return Result.Failure<MyCondominium>(CondominiumErrors.NotFound);
        }

        string? displayName = await managerProfile.GetDisplayNameAsync(request.ManagerUserId, cancellationToken);

        return Result.Success(MyCondominium.From(registration, displayName));
    }
}
