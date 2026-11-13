using MediatR;
using Urban.Condominium.Application.Abstractions;
using Urban.Condominium.Domain.Condominiums;
using Urban.Condominium.Domain.Contacts;
using Urban.Condominium.Domain.Documents;
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
public sealed class RegisterResidentCommandHandler(IResidents residents, IUnits units, ITenantContext tenant, IClock clock)
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

        return Result.Success(ResidentView.From(record));
    }

    private static Result<Registration> Build(RegisterResidentCommand request, Guid condominiumId, DateTimeOffset now)
    {
        DateOnly today = BrazilCalendar.Today(now);

        Result<PersonName> fullName = PersonName.Create(request.FullName);
        Result<Cpf> cpf = Cpf.Create(request.Cpf);
        Result<BirthDate> birthDate = BirthDate.Create(request.BirthDate, today, ResidentErrors.UnderAge);
        Result<PhoneNumber> phone = PhoneNumber.Create(request.Phone);
        Result<ContactEmail?> email = OptionalEmail(request.Email);
        Result<Profession> profession = Profession.Create(request.Profession);
        Result<EmergencyContact> emergencyContact = EmergencyContact.Create(request.EmergencyContact.Name, request.EmergencyContact.Relationship, request.EmergencyContact.Phone);

        foreach (Result part in new Result[] { fullName, cpf, birthDate, phone, email, profession, emergencyContact })
        {
            if (part.IsFailure)
            {
                return Result.Failure<Registration>(part.Error);
            }
        }

        Result<Person> person = Person.RegisterResident(
            condominiumId, fullName.Value, cpf.Value, birthDate.Value, phone.Value, email.Value, profession.Value, emergencyContact.Value, now);
        if (person.IsFailure)
        {
            return Result.Failure<Registration>(person.Error);
        }

        Result<Occupancy> occupancy = Occupancy.Start(condominiumId, person.Value.Id, request.UnitId, request.Kind, request.StartDate, today, now);

        return occupancy.IsFailure
            ? Result.Failure<Registration>(occupancy.Error)
            : Result.Success(new Registration(person.Value, occupancy.Value));
    }

    /// <summary>O e-mail é opcional: vazio é "sem e-mail"; preenchido, tem de ser válido.</summary>
    private static Result<ContactEmail?> OptionalEmail(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return Result.Success<ContactEmail?>(null);
        }

        Result<ContactEmail> email = ContactEmail.Create(text);

        return email.IsFailure ? Result.Failure<ContactEmail?>(email.Error) : Result.Success<ContactEmail?>(email.Value);
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
