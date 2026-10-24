namespace Urban.Identity.Application.Abstractions;

/// <summary>Porta para o hash de senhas. O algoritmo (BCrypt) é detalhe da Infrastructure e pode ser trocado sem tocar nas regras.</summary>
public interface IPasswordHasher
{
    string Hash(string password);

    bool Verify(string password, string hash);

    /// <summary>
    /// Gasta o mesmo tempo de uma verificação real, sem comparar com nada. Usada quando o e-mail não existe, para que a
    /// resposta não demore menos que a de um e-mail existente e denuncie quais e-mails têm conta.
    /// </summary>
    void VerifyDummy(string password);
}