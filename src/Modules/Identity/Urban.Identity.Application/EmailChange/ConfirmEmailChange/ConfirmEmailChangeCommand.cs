using MediatR;
using Urban.SharedKernel;

namespace Urban.Identity.Application.EmailChange.ConfirmEmailChange;

/// <summary>Quem abre o link: o segredo vem da tela pública, sem sessão. O texto do comando o esconde.</summary>
public sealed record ConfirmEmailChangeCommand(string Token) : IRequest<Result>
{
    public override string ToString() => $"{nameof(ConfirmEmailChangeCommand)} {{ Token = * }}";
}
