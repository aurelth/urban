namespace Urban.Api.Endpoints;

public sealed record ChangePasswordRequest(string? CurrentPassword, string? NewPassword)
{
    /// <summary>As senhas não podem aparecer em log nenhum.</summary>
    public override string ToString() => $"{nameof(ChangePasswordRequest)} {{ CurrentPassword = *, NewPassword = * }}";
}
