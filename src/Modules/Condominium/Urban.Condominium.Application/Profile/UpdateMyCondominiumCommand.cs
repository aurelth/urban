using MediatR;
using Urban.Condominium.Application.Registration.RegisterCondominium;
using Urban.SharedKernel;

namespace Urban.Condominium.Application.Profile;

/// <summary>
/// Edição dos campos livres. Não há lugar aqui para CNPJ, CPF, nome completo, data de nascimento ou senha: o que é sensível
/// passa por <see cref="UpdateManagerIdentityCommand"/>, com confirmação de senha.
/// </summary>
/// <param name="Version">A versão que a tela leu; sem ela a edição não sabe o que está substituindo.</param>
public sealed record UpdateMyCondominiumCommand(
    Guid ManagerUserId,
    string? Version,
    string? DisplayName,
    string? Phone,
    AddressInput ManagerAddress,
    string? CondominiumName,
    AddressInput CondominiumAddress) : IRequest<Result<MyCondominium>>;
