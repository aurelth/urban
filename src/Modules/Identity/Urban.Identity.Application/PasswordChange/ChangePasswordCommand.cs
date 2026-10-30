using MediatR;
using Urban.SharedKernel;

namespace Urban.Identity.Application.PasswordChange;

/// <summary>Troca de senha de quem está logado. As senhas não saem em log nenhum: o texto do comando as esconde.</summary>
public sealed record ChangePasswordCommand(Guid UserId, string? CurrentPassword, string? NewPassword) : IRequest<Result>
{
    public override string ToString() => $"{nameof(ChangePasswordCommand)} {{ UserId = {UserId}, CurrentPassword = *, NewPassword = * }}";
}
