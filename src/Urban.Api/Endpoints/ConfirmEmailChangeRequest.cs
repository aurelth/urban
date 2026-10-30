namespace Urban.Api.Endpoints;

public sealed record ConfirmEmailChangeRequest(string? Token)
{
    /// <summary>O segredo do link não pode aparecer em log nenhum.</summary>
    public override string ToString() => $"{nameof(ConfirmEmailChangeRequest)} {{ Token = * }}";
}
