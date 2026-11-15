using MediatR;
using Urban.SharedKernel;

namespace Urban.Identity.Application.Registration.CheckEmailLink;

public sealed record CheckEmailLinkQuery(string Token) : IRequest<Result>
{
    public override string ToString() => $"{nameof(CheckEmailLinkQuery)} {{ Token = * }}";
}
