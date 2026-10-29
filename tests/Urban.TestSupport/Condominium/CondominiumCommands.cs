using Urban.Condominium.Application.Registration.RegisterCondominium;

namespace Urban.TestSupport.Condominium;

/// <summary>Comandos de cadastro prontos para os testes: tudo válido, com CPF e CNPJ únicos a cada chamada.</summary>
public static class CondominiumCommands
{
    public static AddressInput ManagerAddress() =>
        new("89010-000", "Rua XV de Novembro", "1234", "Apto 5", "Centro", "Blumenau", "SC");

    public static AddressInput CondominiumAddress() =>
        new("89201-000", "Rua Princesa Isabel", "88", null, "Atiradores", "Joinville", "SC");

    public static RegisterCondominiumCommand Valid(Guid? managerUserId = null, string? cnpj = null, string? cpf = null) =>
        new(
            managerUserId ?? Guid.CreateVersion7(),
            "Ana",
            new ManagerInput(
                "Ana Maria Silva",
                cpf ?? CondominiumBuilders.UniqueCpf().Value,
                new DateOnly(1985, 3, 12),
                "(47) 99123-4567",
                ManagerAddress()),
            new CondominiumInput(
                "Residencial Vila Verde",
                cnpj ?? CondominiumBuilders.UniqueCnpj().Value,
                CondominiumAddress()));
}
