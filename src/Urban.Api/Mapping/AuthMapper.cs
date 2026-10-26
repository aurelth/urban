using Riok.Mapperly.Abstractions;
using Urban.Api.Endpoints;
using Urban.Identity.Application.Authentication;
using Urban.Identity.Application.Authentication.Me;

namespace Urban.Api.Mapping;

[Mapper]
public static partial class AuthMapper
{
    [MapperIgnoreSource(nameof(AuthTokens.RefreshToken))]
    [MapperIgnoreSource(nameof(AuthTokens.RefreshTokenExpiresAt))]
    public static partial AccessTokenResponse ToResponse(AuthTokens tokens);

    public static partial CurrentUserResponse ToResponse(CurrentUser user);
}