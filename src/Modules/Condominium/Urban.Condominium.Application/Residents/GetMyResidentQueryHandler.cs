using MediatR;
using Urban.Condominium.Application.Abstractions;
using Urban.Condominium.Domain.Condominiums;
using Urban.Condominium.Domain.People;
using Urban.SharedKernel;
using Urban.SharedKernel.Abstractions;

namespace Urban.Condominium.Application.Residents;

/// <summary>
/// O cadastro de quem está logado, achado pela conta e não por id: o morador lê o próprio cadastro (só leitura) e o síndico lê o dele,
/// com ou sem unidade, para registrar onde mora. Não exige vínculo. Conta sem cadastro no condomínio da requisição é "não encontrado".
/// </summary>
public sealed class GetMyResidentQueryHandler(IResidents residents, ITenantContext tenant, ResidentViews views) : IRequestHandler<GetMyResidentQuery, Result<ResidentView>>
{
    public async Task<Result<ResidentView>> Handle(GetMyResidentQuery request, CancellationToken cancellationToken)
    {
        if (tenant.CondominiumId is null)
        {
            return Result.Failure<ResidentView>(CondominiumErrors.NotFound);
        }

        ResidentRecord? record = await residents.FindByUserAsync(request.UserId, cancellationToken);

        return record is null
            ? Result.Failure<ResidentView>(ResidentErrors.NotFound)
            : Result.Success(await views.ViewAsync(record, cancellationToken));
    }
}
