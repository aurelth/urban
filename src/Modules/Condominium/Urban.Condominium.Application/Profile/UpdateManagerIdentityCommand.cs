using MediatR;
using Urban.SharedKernel;

namespace Urban.Condominium.Application.Profile;

/// <summary>
/// Edição dos campos sensíveis do síndico: nome completo, data de nascimento e CPF, com a senha como prova de que quem está
/// na tela é o dono da conta. O CPF ausente mantém o atual. O texto do comando esconde a senha e o CPF.
/// </summary>
public sealed record UpdateManagerIdentityCommand(
    Guid ManagerUserId,
    string? Version,
    string? Password,
    string? FullName,
    DateOnly? BirthDate,
    string? Cpf) : IRequest<Result<MyCondominium>>
{
    public override string ToString() =>
        $"{nameof(UpdateManagerIdentityCommand)} {{ ManagerUserId = {ManagerUserId}, FullName = {FullName}, Password = *, Cpf = *, BirthDate = * }}";
}
