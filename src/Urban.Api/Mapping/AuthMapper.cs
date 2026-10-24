using Riok.Mapperly.Abstractions;
using Urban.Api.Endpoints;
using Urban.Identity.Application.Authentication;

namespace Urban.Api.Mapping;

[Mapper]
public static partial class AuthMapper
{
    [MapperIgnoreSource(nameof(AuthTokens.RefreshToken))]
    [MapperIgnoreSource(nameof(AuthTokens.RefreshTokenExpiresAt))]
    public static partial AccessTokenResponse ToResponse(AuthTokens tokens);
}