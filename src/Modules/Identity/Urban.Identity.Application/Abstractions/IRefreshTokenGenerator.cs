namespace Urban.Identity.Application.Abstractions;

/// <summary>Porta que gera o segredo aleatório de renovação e calcula o hash usado para guardá-lo e procurá-lo.</summary>
public interface IRefreshTokenGenerator
{
    RefreshSecret Generate();

    string Hash(string value);
}
