namespace Urban.EmailDelivery;

public sealed class BrevoOptions
{
    public string ApiKey { get; init; } = string.Empty;

    public string BaseUrl { get; init; } = "https://api.brevo.com/";
}