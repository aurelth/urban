using Urban.Condominium.Domain.Contacts;
using Urban.SharedKernel;

namespace Urban.Condominium.Domain.People;

/// <summary>Quem avisar numa emergência: nome completo, parentesco e telefone com DDD (fixo ou celular).</summary>
public sealed record EmergencyContact
{
    public const int RelationshipMinLength = 2;
    public const int RelationshipMaxLength = 40;

    public static readonly Error NameInvalid = new("condominium.emergency_contact.name_invalid", "Informe o nome e o sobrenome do contato de emergência");

    public static readonly Error RelationshipInvalid = new("condominium.emergency_contact.relationship_invalid", "Informe o parentesco do contato de emergência, com 2 a 40 caracteres");

    public static readonly Error PhoneInvalid = new("condominium.emergency_contact.phone_invalid", "Informe um telefone válido, com DDD, para o contato de emergência");

    private EmergencyContact(PersonName name, string relationship, PhoneNumber phone)
    {
        Name = name;
        Relationship = relationship;
        Phone = phone;
    }

    public PersonName Name { get; }

    public string Relationship { get; }

    public PhoneNumber Phone { get; }

    public static Result<EmergencyContact> Create(string? name, string? relationship, string? phone)
    {
        Result<PersonName> personName = PersonName.Create(name);
        if (personName.IsFailure)
        {
            return Result.Failure<EmergencyContact>(NameInvalid);
        }

        string normalizedRelationship = TextRules.Normalize(relationship);
        if (normalizedRelationship.Length is < RelationshipMinLength or > RelationshipMaxLength || TextRules.HasControlCharacters(normalizedRelationship))
        {
            return Result.Failure<EmergencyContact>(RelationshipInvalid);
        }

        Result<PhoneNumber> number = PhoneNumber.Create(phone);

        return number.IsFailure
            ? Result.Failure<EmergencyContact>(PhoneInvalid)
            : Result.Success(new EmergencyContact(personName.Value, normalizedRelationship, number.Value));
    }
}
