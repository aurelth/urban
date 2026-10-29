using FluentAssertions;
using Urban.Api.Endpoints;
using Urban.Api.Mapping;
using Urban.Condominium.Application.Registration.RegisterCondominium;

namespace Urban.IntegrationTests.Api;

public class CondominiumMapperTests
{
    private static readonly Guid UserId = Guid.CreateVersion7();

    [Fact]
    public void The_request_becomes_the_command_field_by_field_with_the_user_of_the_token()
    {
        RegisterCondominiumRequest request = new(
            "Aninha",
            new ManagerRequest(
                "Ana Maria Silva",
                "52998224725",
                new DateOnly(1985, 3, 12),
                "47991234567",
                new AddressRequest("89010000", "Rua A", "10", "Apto 2", "Centro", "Blumenau", "SC")),
            new CondominiumRequest(
                "Vila Verde",
                "11222333000181",
                new AddressRequest("89201000", "Rua B", "20", null, "Atiradores", "Joinville", "SC")));

        RegisterCondominiumCommand command = CondominiumMapper.ToCommand(request, UserId);

        command.Should().Be(new RegisterCondominiumCommand(
            UserId,
            "Aninha",
            new ManagerInput(
                "Ana Maria Silva",
                "52998224725",
                new DateOnly(1985, 3, 12),
                "47991234567",
                new AddressInput("89010000", "Rua A", "10", "Apto 2", "Centro", "Blumenau", "SC")),
            new CondominiumInput(
                "Vila Verde",
                "11222333000181",
                new AddressInput("89201000", "Rua B", "20", null, "Atiradores", "Joinville", "SC"))));
    }

    [Fact]
    public void Missing_blocks_become_empty_inputs_so_the_domain_can_name_the_first_missing_field()
    {
        RegisterCondominiumCommand command = CondominiumMapper.ToCommand(new RegisterCondominiumRequest(null, null, null), UserId);

        command.ManagerUserId.Should().Be(UserId);
        command.DisplayName.Should().BeNull();
        command.Manager.Should().Be(new ManagerInput(null, null, null, null, new AddressInput(null, null, null, null, null, null, null)));
        command.Condominium.Should().Be(new CondominiumInput(null, null, new AddressInput(null, null, null, null, null, null, null)));
    }

    [Fact]
    public void A_missing_address_inside_a_block_becomes_an_empty_address()
    {
        RegisterCondominiumRequest request = new(
            "Ana",
            new ManagerRequest("Ana Maria", "1", null, "1", null),
            new CondominiumRequest("V", "1", null));

        RegisterCondominiumCommand command = CondominiumMapper.ToCommand(request, UserId);

        command.Manager.Address.Should().Be(new AddressInput(null, null, null, null, null, null, null));
        command.Condominium.Address.Should().Be(new AddressInput(null, null, null, null, null, null, null));
    }

    [Fact]
    public void Printing_the_request_never_shows_the_cpf()
    {
        RegisterCondominiumRequest request = new(
            "Ana",
            new ManagerRequest("Ana Maria Silva", "52998224725", null, null, null),
            null);

        string printed = $"{request} | {request.Manager}";

        printed.Should().NotContain("52998224725");
        printed.Should().Contain("Ana Maria Silva");
    }

    [Fact]
    public void The_response_carries_the_four_public_fields_and_nothing_else()
    {
        Guid id = Guid.CreateVersion7();

        RegisteredCondominiumResponse response = CondominiumMapper.ToResponse(
            new RegisteredCondominium(id, "Vila Verde", "11.222.333/0001-81", "***.982.247-**"));

        response.Should().Be(new RegisteredCondominiumResponse(id, "Vila Verde", "11.222.333/0001-81", "***.982.247-**"));
        typeof(RegisteredCondominiumResponse).GetProperties().Select(p => p.Name)
            .Should().BeEquivalentTo("CondominiumId", "Name", "Cnpj", "ManagerCpf");
    }
}
