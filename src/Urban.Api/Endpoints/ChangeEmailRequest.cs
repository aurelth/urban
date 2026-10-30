namespace Urban.Api.Endpoints;

public sealed record ChangeEmailRequest(string? Password, string? NewEmail)
{
    /// <summary>A senha não pode aparecer em log nenhum.</summary>
    public override string ToString() => $"{nameof(ChangeEmailRequest)} {{ Password = *, NewEmail = {NewEmail} }}";
}
