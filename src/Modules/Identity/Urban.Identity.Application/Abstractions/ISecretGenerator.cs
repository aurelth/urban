namespace Urban.Identity.Application.Abstractions;

/// <summary>Porta que gera segredos aleatórios (renovação de sessão, confirmação de e-mail) e calcula o hash usado para guardá-los e procurá-los.</summary>
public interface ISecretGenerator
{
    GeneratedSecret Generate();

    string Hash(string value);
}
