namespace Urban.Condominium.Application.Abstractions;

/// <summary>
/// Porta para proteger dado pessoal sensível (hoje o CPF) no armazenamento: cifra para guardar e um hash com chave
/// para poder garantir unicidade e buscar sem decifrar.
/// </summary>
public interface IPersonalDataProtector
{
    /// <returns>Texto cifrado, autenticado e com o identificador da chave usada na frente (<c>v1:...</c>).</returns>
    string Protect(string plaintext);

    /// <exception cref="System.Security.Cryptography.CryptographicException">O valor não foi produzido por <see cref="Protect"/>, foi alterado ou a chave não existe mais.</exception>
    string Unprotect(string protectedValue);

    /// <returns>HMAC-SHA256 em hexadecimal minúsculo: igual para o mesmo valor, impossível de refazer sem a chave.</returns>
    string Hash(string value);
}
