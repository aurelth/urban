namespace Urban.Identity.Application.Abstractions;

/// <summary>Porta que gera o segredo aleatório de renovação e o hash que será persistido.</summary>
public interface IRefreshTokenGenerator
{
    RefreshSecret Generate();
}
