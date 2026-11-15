namespace Urban.Api.Endpoints;

public sealed record CheckEmailLinkRequest(string? Token)
{
    public override string ToString() => $"{nameof(CheckEmailLinkRequest)} {{ Token = * }}";
}
