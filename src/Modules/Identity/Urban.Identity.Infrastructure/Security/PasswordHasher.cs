using BCrypt.Net;
using Urban.Identity.Application.Abstractions;

namespace Urban.Identity.Infrastructure.Security;

/// <summary>
/// Hash de senha com BCrypt. Usa a variante "enhanced", que passa a senha por SHA-384 antes: o BCrypt puro só lê os
/// primeiros 72 bytes e ignoraria o resto das senhas longas (a política aceita até 128 caracteres).
/// </summary>
public sealed class PasswordHasher : IPasswordHasher
{
    private const HashType PreHash = HashType.SHA384;

    private readonly int _workFactor;
    private readonly string _dummyHash;

    public PasswordHasher(PasswordHashingOptions options)
    {
        if (options.WorkFactor is < PasswordHashingOptions.MinimumWorkFactor or > PasswordHashingOptions.MaximumWorkFactor)
        {
            throw new ArgumentOutOfRangeException(
                nameof(options),
                options.WorkFactor,
                $"O custo do BCrypt deve ficar entre {PasswordHashingOptions.MinimumWorkFactor} e {PasswordHashingOptions.MaximumWorkFactor}");
        }

        _workFactor = options.WorkFactor;
        _dummyHash = Hash(Guid.NewGuid().ToString("N"));
    }

    public string Hash(string password) => BCrypt.Net.BCrypt.EnhancedHashPassword(password, PreHash, _workFactor);

    public bool Verify(string password, string hash) => BCrypt.Net.BCrypt.EnhancedVerify(password, hash, PreHash);

    public void VerifyDummy(string password) => _ = Verify(password, _dummyHash);
}