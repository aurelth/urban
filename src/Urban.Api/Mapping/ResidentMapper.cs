using Riok.Mapperly.Abstractions;
using Urban.Api.Endpoints;
using Urban.Condominium.Application.Abstractions;
using Urban.Condominium.Application.Residents;
using Urban.Condominium.Domain.Occupancies;

namespace Urban.Api.Mapping;

[Mapper]
public static partial class ResidentMapper
{
    private static readonly EmergencyContactRequest NoContact = new(null, null, null);

    // ---- Do corpo da requisição para o comando ----

    /// <summary>Um contato ausente vira um contato vazio: quem diz qual campo falta é o Domain, com o código do campo.</summary>
    public static RegisterResidentCommand ToCommand(ResidentRequest request, Guid actorUserId)
    {
        EmergencyContactRequest contact = request.EmergencyContact ?? NoContact;

        return new RegisterResidentCommand(
            actorUserId,
            request.FullName,
            request.Cpf,
            request.BirthDate,
            request.Phone,
            request.Email,
            request.Profession,
            new EmergencyContactInput(contact.Name, contact.Relationship, contact.Phone),
            request.UnitId,
            ParseKind(request.Kind),
            request.StartDate);
    }

    public static UpdateResidentCommand ToCommand(Guid residentId, ResidentUpdateRequest request, Guid actorUserId)
    {
        EmergencyContactRequest contact = request.EmergencyContact ?? NoContact;

        return new UpdateResidentCommand(
            actorUserId,
            residentId,
            request.Version,
            request.FullName,
            request.Cpf,
            request.BirthDate,
            request.Phone,
            request.Email,
            request.Profession,
            new EmergencyContactInput(contact.Name, contact.Relationship, contact.Phone));
    }

    public static AddOccupancyCommand ToCommand(Guid residentId, OccupancyRequest request, Guid actorUserId) =>
        new(actorUserId, residentId, request.UnitId, ParseKind(request.Kind), request.StartDate);

    public static EndOccupancyCommand ToCommand(Guid residentId, Guid occupancyId, OccupancyEndRequest request, Guid actorUserId) =>
        new(actorUserId, residentId, occupancyId, request.EndDate);

    // ---- Do resultado para a resposta ----

    public static partial ResidentResponse ToResponse(ResidentView view);

    public static partial ResidentListResponse ToResponse(ResidentListing listing);

    /// <returns>Falso quando o texto não é um estado conhecido; vazio é o padrão (todos).</returns>
    public static bool TryParseStatus(string? text, out ResidentStatusFilter status)
    {
        switch (text?.Trim().ToLowerInvariant())
        {
            case null or "":
            case "all":
                status = ResidentStatusFilter.All;
                return true;
            case "active":
                status = ResidentStatusFilter.Active;
                return true;
            case "former":
                status = ResidentStatusFilter.Former;
                return true;
            default:
                status = default;
                return false;
        }
    }

    // ---- Texto da API <-> enum (camelCase). Valor desconhecido vira 0, que o Domain recusa com o código do campo ----

    public static OccupancyKind ParseKind(string? text) => text?.Trim().ToLowerInvariant() switch
    {
        "owner" => OccupancyKind.Owner,
        "tenant" => OccupancyKind.Tenant,
        _ => 0,
    };

    private static string KindName(OccupancyKind kind) => kind switch
    {
        OccupancyKind.Owner => "owner",
        OccupancyKind.Tenant => "tenant",
        _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, "Tipo de vínculo desconhecido."),
    };
}
