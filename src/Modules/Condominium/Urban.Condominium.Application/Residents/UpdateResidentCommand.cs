using MediatR;
using Urban.SharedKernel;

namespace Urban.Condominium.Application.Residents;

/// <summary>
/// A edição leva todos os dados do cadastro, como na tela. O CPF é opcional: a tela só conhece o CPF mascarado, então em branco
/// é "continua o mesmo"; preenchido, troca o CPF.
/// </summary>
/// <param name="Version">A versão que a tela leu; sem ela a edição não sabe o que está substituindo.</param>
public sealed record UpdateResidentCommand(
    Guid ActorUserId,
    Guid ResidentId,
    string? Version,
    string? FullName,
    string? Cpf,
    DateOnly? BirthDate,
    string? Phone,
    string? Email,
    string? Profession,
    EmergencyContactInput EmergencyContact) : IRequest<Result<ResidentView>>
{
    /// <summary>O CPF e os demais dados pessoais não podem aparecer num log que imprima o comando.</summary>
    public override string ToString() => $"{nameof(UpdateResidentCommand)} {{ ResidentId = {ResidentId} }}";
}
