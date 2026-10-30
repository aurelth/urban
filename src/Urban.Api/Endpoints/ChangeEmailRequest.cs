namespace Urban.Api.Endpoints;

public sealed record ChangeEmailRequest(string? Password, string? NewEmail)
{
    /// <summary>A senha não pode aparecer em log nenhum: o texto do pedido nem a menciona.</summary>
    public override string ToString() => $"{nameof(ChangeEmailRequest)} {{ NewEmail = {NewEmail} }}";
}
