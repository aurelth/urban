using Urban.Condominium.Domain.Contacts;
using Urban.Condominium.Domain.Documents;
using Urban.Condominium.Domain.People;
using Urban.SharedKernel;

namespace Urban.Condominium.Application.Residents;

/// <summary>
/// Os dados pessoais de um morador já validados e normalizados. Cadastro e edição digitam os mesmos campos e valem as mesmas
/// regras, então a conferência mora num lugar só: vale o primeiro erro, na ordem da tela, e o código diz o campo.
/// </summary>
internal sealed record ResidentData(
    PersonName FullName,
    Cpf Cpf,
    BirthDate BirthDate,
    PhoneNumber Phone,
    ContactEmail? Email,
    Profession Profession,
    EmergencyContact EmergencyContact)
{
    /// <param name="currentCpf">Na edição, o CPF que a pessoa já tem: a tela só vê o CPF mascarado, então CPF em branco é "não mudou".</param>
    internal static Result<ResidentData> Parse(
        string? fullName,
        string? cpf,
        DateOnly? birthDate,
        string? phone,
        string? email,
        string? profession,
        EmergencyContactInput emergencyContact,
        DateOnly today,
        Cpf? currentCpf = null)
    {
        Result<PersonName> parsedName = PersonName.Create(fullName);
        Result<Cpf> parsedCpf = currentCpf is not null && string.IsNullOrWhiteSpace(cpf) ? Result.Success(currentCpf) : Cpf.Create(cpf);
        Result<BirthDate> parsedBirthDate = BirthDate.Create(birthDate, today, ResidentErrors.UnderAge);
        Result<PhoneNumber> parsedPhone = PhoneNumber.Create(phone);
        Result<ContactEmail?> parsedEmail = OptionalEmail(email);
        Result<Profession> parsedProfession = Profession.Create(profession);
        Result<EmergencyContact> parsedContact = EmergencyContact.Create(emergencyContact.Name, emergencyContact.Relationship, emergencyContact.Phone);

        foreach (Result part in new Result[] { parsedName, parsedCpf, parsedBirthDate, parsedPhone, parsedEmail, parsedProfession, parsedContact })
        {
            if (part.IsFailure)
            {
                return Result.Failure<ResidentData>(part.Error);
            }
        }

        return Result.Success(new ResidentData(
            parsedName.Value, parsedCpf.Value, parsedBirthDate.Value, parsedPhone.Value, parsedEmail.Value, parsedProfession.Value, parsedContact.Value));
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
}
