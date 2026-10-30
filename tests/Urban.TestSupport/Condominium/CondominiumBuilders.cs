using System.Globalization;
using System.Security.Cryptography;
using Urban.Condominium.Application.Abstractions;
using Urban.Condominium.Domain.Contacts;
using Urban.Condominium.Domain.Documents;
using Urban.Condominium.Domain.People;
using Urban.SharedKernel;
using CondominiumEntity = Urban.Condominium.Domain.Condominiums.Condominium;

namespace Urban.TestSupport.Condominium;

/// <summary>
/// Monta condomínios e pessoas válidos usando só a API pública do Domain. Os números de CPF e CNPJ saem do próprio
/// Domain (o dígito verificador é achado por tentativa), então o teste nunca repete o algoritmo.
/// </summary>
public static class CondominiumBuilders
{
    public static readonly DateTimeOffset Now = new(2026, 10, 29, 12, 0, 0, TimeSpan.Zero);

    public const string SampleCpfDigits = "52998224725";

    public static Address SampleAddress() =>
        Address.Create("Rua XV de Novembro", "1234", "Sala 2", "Centro", "Blumenau", "SC", "89010-000").Value;

    public static Cpf UniqueCpf()
    {
        string root = RandomDigits(9);
        for (int check = 0; check < 100; check++)
        {
            Result<Cpf> candidate = Cpf.Create(root + check.ToString("00", CultureInfo.InvariantCulture));
            if (candidate.IsSuccess)
            {
                return candidate.Value;
            }
        }

        return UniqueCpf();
    }

    public static Cnpj UniqueCnpj()
    {
        string root = RandomDigits(12);
        for (int check = 0; check < 100; check++)
        {
            Result<Cnpj> candidate = Cnpj.Create(root + check.ToString("00", CultureInfo.InvariantCulture));
            if (candidate.IsSuccess)
            {
                return candidate.Value;
            }
        }

        return UniqueCnpj();
    }

    public static CondominiumEntity Condominium(Guid? createdBy = null, Cnpj? cnpj = null, Address? address = null) =>
        CondominiumEntity.Register(
            "Residencial Vila Verde",
            cnpj ?? UniqueCnpj(),
            address ?? SampleAddress(),
            createdBy ?? Guid.CreateVersion7(),
            Now).Value;

    public static Person Manager(Guid condominiumId, Guid? userId = null, Cpf? cpf = null, Address? address = null) =>
        Person.RegisterManager(
            condominiumId,
            userId ?? Guid.CreateVersion7(),
            PersonName.Create("Ana Maria Silva").Value,
            cpf ?? UniqueCpf(),
            BirthDate.Create(new DateOnly(1985, 3, 12), new DateOnly(2026, 10, 29)).Value,
            PhoneNumber.Create("(47) 99123-4567").Value,
            address ?? SampleAddress(),
            Now).Value;

    /// <summary>O cadastro de um síndico como a leitura do banco o entrega: condomínio, síndico e a versão (o xmin dos dois) juntos.</summary>
    public static ManagerCondominium Registration(Guid? userId = null, string version = "10.20")
    {
        Guid managerUserId = userId ?? Guid.CreateVersion7();
        CondominiumEntity condominium = Condominium(managerUserId);

        return new ManagerCondominium(condominium, Manager(condominium.Id, managerUserId), version);
    }

    private static string RandomDigits(int count) =>
        string.Concat(Enumerable.Range(0, count).Select(_ => RandomNumberGenerator.GetInt32(10).ToString(CultureInfo.InvariantCulture)));
}
