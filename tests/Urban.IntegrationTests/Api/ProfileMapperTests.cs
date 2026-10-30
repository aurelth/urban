using FluentAssertions;
using Urban.Api.Endpoints;
using Urban.Api.Mapping;
using Urban.Condominium.Application.Profile;
using Urban.Condominium.Application.Registration.RegisterCondominium;

namespace Urban.IntegrationTests.Api;

public class ProfileMapperTests
{
    private static readonly Guid UserId = Guid.CreateVersion7();

    [Fact]
    public void The_free_fields_request_becomes_the_command_field_by_field_with_the_user_of_the_token()
    {
        UpdateMyCondominiumRequest request = new(
            "7.9",
            "Aninha",
            "47991234567",
            new AddressRequest("89010000", "Rua A", "10", "Apto 2", "Centro", "Blumenau", "SC"),
            "Vila Nova",
            new AddressRequest("89201000", "Rua B", "20", null, "Atiradores", "Joinville", "SC"));

        UpdateMyCondominiumCommand command = CondominiumMapper.ToCommand(request, UserId);

        command.Should().Be(new UpdateMyCondominiumCommand(
            UserId,
            "7.9",
            "Aninha",
            "47991234567",
            new AddressInput("89010000", "Rua A", "10", "Apto 2", "Centro", "Blumenau", "SC"),
            "Vila Nova",
            new AddressInput("89201000", "Rua B", "20", null, "Atiradores", "Joinville", "SC")));
    }

    [Fact]
    public void Missing_addresses_become_empty_inputs_so_the_domain_names_the_first_missing_field()
    {
        UpdateMyCondominiumCommand command = CondominiumMapper.ToCommand(new UpdateMyCondominiumRequest(null, null, null, null, null, null), UserId);

        command.ManagerUserId.Should().Be(UserId);
        command.ManagerAddress.Should().Be(new AddressInput(null, null, null, null, null, null, null));
        command.CondominiumAddress.Should().Be(new AddressInput(null, null, null, null, null, null, null));
    }

    [Fact]
    public void The_identity_request_becomes_the_command_with_the_user_of_the_token()
    {
        UpdateManagerIdentityRequest request = new("7.9", "Senha-segura-123", "Maria Souza Lima", new DateOnly(1990, 7, 1), "11144477735");

        UpdateManagerIdentityCommand command = CondominiumMapper.ToCommand(request, UserId);

        command.Should().Be(new UpdateManagerIdentityCommand(UserId, "7.9", "Senha-segura-123", "Maria Souza Lima", new DateOnly(1990, 7, 1), "11144477735"));
    }

    [Fact]
    public void The_request_types_do_not_have_a_field_for_what_cannot_be_changed_here()
    {
        string[] forbidden = ["Cnpj", "Cpf", "FullName", "BirthDate", "UserId", "CondominiumId", "Id"];

        typeof(UpdateMyCondominiumRequest).GetProperties().Select(p => p.Name).Should().NotContain(forbidden);
        typeof(UpdateMyCondominiumCommand).GetProperties().Select(p => p.Name).Should().NotContain(forbidden.Where(f => f != "UserId"));
    }

    [Fact]
    public void The_response_has_the_condominium_and_the_manager_with_the_cpf_masked()
    {
        Guid id = Guid.CreateVersion7();
        AddressView manager = new("89010000", "Rua A", "10", "Apto 2", "Centro", "Blumenau", "SC");
        AddressView condominium = new("89201000", "Rua B", "20", null, "Atiradores", "Joinville", "SC");
        MyCondominium mine = new(
            id,
            "7.9",
            "Aninha",
            new ManagerView("Ana Maria Silva", "***.982.247-**", new DateOnly(1985, 3, 12), "47991234567", manager),
            new CondominiumView("Vila Verde", "11.222.333/0001-81", condominium));

        MyCondominiumResponse response = CondominiumMapper.ToResponse(mine);

        response.CondominiumId.Should().Be(id);
        response.Version.Should().Be("7.9");
        response.DisplayName.Should().Be("Aninha");
        response.Manager.MaskedCpf.Should().Be("***.982.247-**");
        response.Manager.FullName.Should().Be("Ana Maria Silva");
        response.Manager.Address.City.Should().Be("Blumenau");
        response.Condominium.Cnpj.Should().Be("11.222.333/0001-81");
        response.Condominium.Address.Complement.Should().BeNull();
    }

    [Fact]
    public void Printing_the_requests_never_shows_passwords_or_the_cpf()
    {
        UpdateManagerIdentityRequest identity = new("1.1", "Senha-segura-123", "Maria Souza Lima", null, "11144477735");
        ChangePasswordRequest password = new("Senha-antiga-123", "Senha-nova-456");
        ChangeEmailRequest email = new("Senha-segura-123", "nova@example.com");

        string printed = $"{identity} | {password} | {email}";

        printed.Should().NotContain("Senha-segura-123").And.NotContain("11144477735")
            .And.NotContain("Senha-antiga-123").And.NotContain("Senha-nova-456");
        printed.Should().Contain("Maria Souza Lima").And.Contain("nova@example.com");
    }

    [Fact]
    public void Printing_the_commands_never_shows_passwords_or_the_cpf()
    {
        UpdateManagerIdentityCommand identity = new(UserId, "1.1", "Senha-segura-123", "Maria Souza Lima", null, "11144477735");

        identity.ToString().Should().NotContain("Senha-segura-123").And.NotContain("11144477735").And.Contain("Maria Souza Lima");
    }
}
