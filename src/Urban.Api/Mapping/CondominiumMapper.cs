using Riok.Mapperly.Abstractions;
using Urban.Api.Endpoints;
using Urban.Condominium.Application.Registration.RegisterCondominium;
using Urban.SharedKernel.Abstractions;

namespace Urban.Api.Mapping;

[Mapper]
public static partial class CondominiumMapper
{
    private static readonly AddressRequest NoAddress = new(null, null, null, null, null, null, null);

    /// <summary>Bloco ausente vira bloco vazio: quem diz qual campo falta é o Domain, com o código do campo.</summary>
    public static RegisterCondominiumCommand ToCommand(RegisterCondominiumRequest request, Guid managerUserId)
    {
        ManagerRequest manager = request.Manager ?? new ManagerRequest(null, null, null, null, null);
        CondominiumRequest condominium = request.Condominium ?? new CondominiumRequest(null, null, null);

        return new RegisterCondominiumCommand(
            managerUserId,
            request.DisplayName,
            new ManagerInput(manager.FullName, manager.Cpf, manager.BirthDate, manager.Phone, MapAddress(manager.Address ?? NoAddress)),
            new CondominiumInput(condominium.Name, condominium.Cnpj, MapAddress(condominium.Address ?? NoAddress)));
    }

    public static partial RegisteredCondominiumResponse ToResponse(RegisteredCondominium registered);

    public static partial PostalCodeResponse ToResponse(PostalCodeAddress address);

    private static partial AddressInput MapAddress(AddressRequest address);
}
