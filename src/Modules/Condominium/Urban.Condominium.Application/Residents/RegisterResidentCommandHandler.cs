using MediatR;
using Urban.Condominium.Application.Abstractions;
using Urban.Condominium.Domain.Condominiums;
using Urban.Condominium.Domain.Occupancies;
using Urban.Condominium.Domain.People;
using Urban.Condominium.Domain.Units;
using Urban.SharedKernel;
using Urban.SharedKernel.Abstractions;

namespace Urban.Condominium.Application.Residents;

/// <summary>
/// Cadastra um morador adulto com o primeiro vínculo, no condomínio da requisição. Valida tudo em memória antes de falar com o
/// banco (vale o primeiro erro, cujo código diz o campo); só então confere a unidade. O CPF repetido é decidido na gravação,
/// pelo índice único, que resiste a dois cadastros ao mesmo tempo. Cadastrar não cria conta nem manda e-mail: o acesso é o convite.
/// </summary>
public sealed class RegisterResidentCommandHandler(IResidents residents, IUnits units, ITenantContext tenant, IClock clock, ResidentViews views)
    : IRequestHandler<RegisterResidentCommand, Result<ResidentView>>
{
    public async Task<Result<ResidentView>> Handle(RegisterResidentCommand request, CancellationToken cancellationToken)
    {
        if (tenant.CondominiumId is not { } condominiumId)
        {
            return Result.Failure<ResidentView>(CondominiumErrors.NotFound);
        }

        DateTimeOffset now = clock.UtcNow;
        Result<Registration> built = Build(request, condominiumId, now);
        if (built.IsFailure)
        {
            return Result.Failure<ResidentView>(built.Error);
        }

        Result<CondominiumUnit> unit = await FindOpenUnitAsync(request.UnitId, cancellationToken);
        if (unit.IsFailure)
        {
            return Result.Failure<ResidentView>(unit.Error);
        }

        Person person = built.Value.Person;
        ChangeLogEntry entry = new(condominiumId, request.ActorUserId, ChangeKind.Resident, FilledIn(person), now, person.Id);
        Result<string> saved = await residents.AddAsync(person, built.Value.Occupancy, entry, cancellationToken);
        if (saved.IsFailure)
        {
            return Result.Failure<ResidentView>(saved.Error);
        }

        ResidentRecord record = new(person, [new ResidentOccupancy(built.Value.Occupancy, unit.Value.Block, unit.Value.Number)], saved.Value);

        return Result.Success(await views.ViewAsync(record, cancellationToken));
    }

    private static Result<Registration> Build(RegisterResidentCommand request, Guid condominiumId, DateTimeOffset now)
    {
        DateOnly today = BrazilCalendar.Today(now);

        Result<ResidentData> data = ResidentData.Parse(
            request.FullName, request.Cpf, request.BirthDate, request.Phone, request.Email, request.Profession, request.EmergencyContact, today);
        if (data.IsFailure)
        {
            return Result.Failure<Registration>(data.Error);
        }

        Result<Person> person = Person.RegisterResident(
            condominiumId, data.Value.FullName, data.Value.Cpf, data.Value.BirthDate, data.Value.Phone, data.Value.Email, data.Value.Profession, data.Value.EmergencyContact, now);
        if (person.IsFailure)
        {
            return Result.Failure<Registration>(person.Error);
        }

        Result<Occupancy> occupancy = Occupancy.Start(condominiumId, person.Value.Id, request.UnitId, request.Kind, request.StartDate, today, now);

        return occupancy.IsFailure
            ? Result.Failure<Registration>(occupancy.Error)
            : Result.Success(new Registration(person.Value, occupancy.Value));
    }

    /// <summary>A unidade tem de existir no condomínio da requisição (a de outro condomínio é "não encontrada") e estar ativa.</summary>
    private async Task<Result<CondominiumUnit>> FindOpenUnitAsync(Guid unitId, CancellationToken cancellationToken)
    {
        UnitRecord? record = await units.FindAsync(unitId, cancellationToken);
        if (record is null)
        {
            return Result.Failure<CondominiumUnit>(UnitErrors.NotFound);
        }

        return record.Unit.IsActive
            ? Result.Success(record.Unit)
            : Result.Failure<CondominiumUnit>(UnitErrors.Deactivated);
    }

    /// <summary>Só os nomes dos campos preenchidos, nunca os valores (o CPF não pode aparecer em lugar nenhum fora da coluna cifrada).</summary>
    private static List<string> FilledIn(Person person)
    {
        List<string> fields = ["fullName", "cpf", "birthDate", "phone"];
        if (person.Email is not null)
        {
            fields.Add("email");
        }

        fields.Add("profession");
        fields.Add("emergencyContact");
        fields.Add("occupancy");

        return fields;
    }

    private sealed record Registration(Person Person, Occupancy Occupancy);
}
