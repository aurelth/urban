using MediatR;
using Urban.SharedKernel;

namespace Urban.Identity.Application.EmailChange.RequestEmailChange;

/// <summary>Pedido de troca de e-mail de quem está logado. A senha não sai em log nenhum: o texto do comando a esconde.</summary>
public sealed record RequestEmailChangeCommand(Guid UserId, string? Password, string? NewEmail) : IRequest<Result>
{
    public override string ToString() => $"{nameof(RequestEmailChangeCommand)} {{ UserId = {UserId}, Password = *, NewEmail = {NewEmail} }}";
}
